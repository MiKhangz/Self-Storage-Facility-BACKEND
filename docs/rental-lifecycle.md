# Rental lifecycle — from registration to the unit coming back

How registration, payment, contract, monthly billing, handover, visits and return fit together, as the code
does it (after the sprint-review renames of 18 Oct 2026). Each step names the API call, the tables it writes and
the status each row moves to. Roles: **C** customer, **S** facility staff, **M** facility manager,
**B** business operations manager, **A** system administrator; staff and managers act only on facilities they work at.

Vocabulary: **handover** starts the rental (keys handed over); a **check-in** is the customer entering and
leaving their unit during the rental (check-out when they leave); **return** ends it (the unit is given back).

| # | Step | Who · call | Table · change |
|---|---|---|---|
| 1 | **Register and sign in** | C · `POST /api/auth/register`, `/verify-email`, `/login` | `User` inserted `Pending`; the emailed link makes it `Active` (Development: `Active` at once). Login records an `ActivityLog` row. |
| 2 | **Search and reserve** | C · `GET /api/facilities`, `/api/unit-types/{id}/quote` · `POST /api/reservations` | `Reservation` inserted `Pending` for a facility, a unit **type**, move-in date and months. No unit is held yet: the request is refused only if no unit of that type is `Available`. |
| 3 | **Deposit** | system on step 2 · C `POST /api/invoices/{id}/payments/online` | `Invoice` `DEP…` (`ReservationId`, no contract) inserted `Issued`, due on the move-in date, amount = policy rent × `DepositMonths`. Payment: `Payment` `Pending` → `Succeeded`; invoice `PaidAmount` += amount, `Issued` → `Paid`. |
| 4 | **Assign a unit** | S/M · `POST /api/reservations/{id}/assign-unit` | `Reservation` `Pending` → `Confirmed`, `StorageUnitId` set; `StorageUnit` `Available` → `Reserved`. |
| 5 | **Contract** | S/M · `POST /api/contracts` · C `POST /api/contracts/{id}/sign` | `Contract` inserted `Draft` with rent fixed from the policy in force on the move-in date (rule 4) and the rent day. Signing sets `SignedAt`; the contract stays `Draft`. |
| 6 | **Handover** | S/M · `POST /api/handovers` (refused until the deposit is paid and the contract signed) | `Handover` inserted `Completed` (lock serial, card number, condition, photos in `HandoverPhoto`); `Contract` `Draft` → `Active`; `StorageUnit` `Reserved` → `Occupied`; `Reservation` → `HandedOver`; `AccessCode` issued (hash stored, digits shown once). **Undo**: `POST /api/handovers/{id}/cancel` → `Handover` `Cancelled`, `Contract` `Terminated`, codes off, unit `Available`, reservation `Confirmed` without a unit (back to step 4). |
| 7a | **Check-ins** | S/M · `POST /api/check-ins/check-in`, `/{id}/check-out` | `CheckIn` inserted with `CheckInAt`; check-out sets `CheckOutAt`. Only for an `Active` contract, one open visit at a time. `AccessCode`/`AccessLog` are separate and unchanged. |
| 7b | **Monthly invoice** | system · `BillingWorker` (hourly, idempotent per day) | For each `Active`/`Overdue`/`Suspended`/`PendingReturn` contract: one `Invoice` `INV<yyyyMM>C<id>` + `InvoiceItem` for `MonthlyRent`, issued 7 days before it falls due on the contract's `PaymentDay`. Reminder email 3 days before. |
| 7c | **Overdue** | system · same job · M/B `POST /api/overdue-cases/{id}/lockout`, `/restore-access` | Unpaid after the due date → `Invoice` `Overdue`. After `GraceDays` → `OverdueCase` opened, `Contract` → `Overdue`; a late-fee line grows `LateFeePercentPerWeek` per full week. At `SuspendAfterDays` → lock-out: `Contract` `Suspended`, access codes suspended. Paying in full resolves the case and restores access. |
| 8 | **Return request** | C · `POST /api/returns` | `UnitReturn` inserted `Requested` with the planned date (today up to 15 days ahead, early or at term — no notice period) and `MoveOutDeadline` = request date + 15; `Contract` `Active` → `PendingReturn`. |
| 8a | **Move-out deadline** | system · `BillingWorker` | A `Requested`/`Approved` return past its `MoveOutDeadline`: the contract's access codes off, `UnitReturn.AccessLockedAt` set. Staff clear the unit out, then continue at step 9. |
| 9 | **Inspection** | S/M · `POST /api/returns/{id}/approve`, `/inspection` | Approve: `UnitReturn` → `Approved` with an inspection slot. Inspect: `ReturnInspection` inserted `Completed` (condition, cleaning, repair, other deductions); `UnitReturn` → `Inspected`. |
| 10 | **Deposit settled** | B · `POST /api/returns/{id}/settle-deposit` | Refund = deposit − damage − outstanding invoices − early-termination penalty, never below zero (any excess is the customer's shortfall). Penalty = `DepositAmount × EarlyTerminationPercent / 100` when the planned date is before `Contract.EndDate`, rate from the policy in force on the move-in date; it is added to `ReturnInspection.OtherDeduction` and returned as its own line. `ReturnInspection.RefundAmount` set; `UnitReturn` → `Completed`; `Contract` → `Closed`; access codes off. |
| 11 | **Unit free again** | system on step 10 | `StorageUnit` `Occupied` → `Available` (`Maintenance` if the inspection found major damage). |

Online payment goes through VNPay when its sandbox keys are set; otherwise it is simulated in Development.
Email is sent over SMTP when `Smtp:Host` is set, otherwise it goes to the API log.

Deposits owed back (a refundable cancellation, or a return settled with a refund) are recorded as Pending
`DepositRefund`s; Business Operations mark each one Paid once the customer has the money (Admin › Deposit refunds).
