# Rental lifecycle — from registration to the unit coming back

How registration, payment, contract, monthly billing, check-in, visits and handover fit together, as the code
does it (after the sprint-review renames of 18 Oct 2026). Each step names the API call, the tables it writes and
the status each row moves to. Roles: **C** customer, **S** facility staff, **M** facility manager,
**B** business operations manager, **A** system administrator; staff and managers act only on facilities they work at.

Vocabulary: **check-in** starts the rental (keys handed over); a **unit visit** is the customer entering and
leaving their unit during the rental; **handover** ends it (the unit is given back). "Check-in" is deliberately used
for both the rental start and a visit.

| # | Step | Who · call | Table · change |
|---|---|---|---|
| 1 | **Register and sign in** | C · `POST /api/auth/register`, `/verify-email`, `/login` | `User` inserted `Pending`; the emailed link makes it `Active` (Development: `Active` at once). Login records an `ActivityLog` row. |
| 2 | **Search and reserve** | C · `GET /api/facilities`, `/api/unit-types/{id}/quote` · `POST /api/reservations` | `Reservation` inserted `Pending` for a facility, a unit **type**, move-in date and months. No unit is held yet: the request is refused only if no unit of that type is `Available`. |
| 3 | **Deposit** | system on step 2 · C `POST /api/invoices/{id}/payments/online` | `Invoice` `DEP…` (`ReservationId`, no contract) inserted `Issued`, due on the move-in date, amount = policy rent × `DepositMonths`. Payment: `Payment` `Pending` → `Succeeded`; invoice `PaidAmount` += amount, `Issued` → `Paid`. |
| 4 | **Assign a unit** | S/M · `POST /api/reservations/{id}/assign-unit` | `Reservation` `Pending` → `Confirmed`, `StorageUnitId` set; `StorageUnit` `Available` → `Reserved`. |
| 5 | **Contract** | S/M · `POST /api/contracts` · C `POST /api/contracts/{id}/sign` | `Contract` inserted `Draft` with rent fixed from the policy in force on the move-in date (rule 4) and the rent day. Signing sets `SignedAt`; the contract stays `Draft`. |
| 6 | **Check-in** | S/M · `POST /api/check-ins` (refused until the deposit is paid and the contract signed) | `CheckIn` inserted `Completed` (lock serial, card number, condition, photos in `CheckInPhoto`); `Contract` `Draft` → `Active`; `StorageUnit` `Reserved` → `Occupied`; `Reservation` → `CheckedIn`; `AccessCode` issued (hash stored, digits shown once). **Undo**: `POST /api/check-ins/{id}/cancel` → `CheckIn` `Cancelled`, `Contract` `Terminated`, codes off, unit `Available`, reservation `Confirmed` without a unit (back to step 4). |
| 7a | **Unit visits** | S/M · `POST /api/unit-visits/check-in`, `/{id}/check-out` | `UnitVisit` inserted with `CheckInAt`; check-out sets `CheckOutAt`. Only for an `Active` contract, one open visit at a time. `AccessCode`/`AccessLog` are separate and unchanged. |
| 7b | **Monthly invoice** | system · `BillingWorker` (hourly, idempotent per day) | For each `Active`/`Overdue`/`Suspended`/`PendingHandover` contract: one `Invoice` `INV<yyyyMM>C<id>` + `InvoiceItem` for `MonthlyRent`, issued 7 days before it falls due on the contract's `PaymentDay`. Reminder email 3 days before. |
| 7c | **Overdue** | system · same job · M/B `POST /api/overdue-cases/{id}/lockout`, `/restore-access` | Unpaid after the due date → `Invoice` `Overdue`. After `GraceDays` → `OverdueCase` opened, `Contract` → `Overdue`; a late-fee line grows `LateFeePercentPerWeek` per full week. At `SuspendAfterDays` → lock-out: `Contract` `Suspended`, access codes suspended. Paying in full resolves the case and restores access. |
| 8 | **Handover request** | C · `POST /api/handovers` | `Handover` inserted `Requested` with the planned date (any date from today, early or at term — no notice period); `Contract` `Active` → `PendingHandover`. |
| 9 | **Inspection** | S/M · `POST /api/handovers/{id}/approve`, `/inspection` | Approve: `Handover` → `Approved` with an inspection slot. Inspect: `HandoverInspection` inserted `Completed` (condition, cleaning, repair, other deductions); `Handover` → `Inspected`. |
| 10 | **Deposit settled** | B · `POST /api/handovers/{id}/settle-deposit` | Refund = deposit − damage − outstanding invoices − early-termination penalty, never below zero (any excess is the customer's shortfall). Penalty = `DepositAmount × EarlyTerminationPercent / 100` when the planned date is before `Contract.EndDate`, rate from the policy in force on the move-in date; it is added to `HandoverInspection.OtherDeduction` and returned as its own line. `HandoverInspection.RefundAmount` set; `Handover` → `Completed`; `Contract` → `Closed`; access codes off. |
| 11 | **Unit free again** | system on step 10 | `StorageUnit` `Occupied` → `Available` (`Maintenance` if the inspection found major damage). |

Online payment goes through VNPay when its sandbox keys are set; otherwise it is simulated in Development.
Email is sent over SMTP when `Smtp:Host` is set, otherwise it goes to the API log.
