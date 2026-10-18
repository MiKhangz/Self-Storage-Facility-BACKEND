# SPRINT_REVIEW_FIXES — actions from the review of 1 Oct 2026

Source: `Biên bản họp` — sprint review with Đỗ Tấn Nhàn (instructor), NVH 610.

Working file for Claude Code, backend repo. **Read `CLAUDE.md` first.** This file overrides it where
the two disagree, and section 8 lists the `CLAUDE.md` edits that must be made to remove the
disagreement.

Every decision below was confirmed with Khang on 17 Oct. Items under "Open questions" were **not**
confirmed — do not implement them, do not guess them.


## Status — implemented 18 Oct 2026

| Task | Done | Decision taken with Khang on 18 Oct |
|---|---|---|
| SR-01 | ✓ migration `RenameHandoverToCheckIn` | `HandoverPhoto` → `CheckInPhoto` and `TaskType.Handover` → `CheckIn` too ("all the way down") |
| SR-02 | ✓ | `GET` by id already existed. **Cancel** = check-in `Cancelled`, contract `Terminated`, codes revoked, unit `Available`, reservation `Confirmed` with its unit cleared; `assign-unit` accepts such a reservation again |
| SR-03 | ✓ migration `RenameMoveOutToHandover` | contract status `PendingMoveOut` → `PendingHandover`, column `PlannedMoveOutDate` → `PlannedHandoverDate` |
| SR-04 | ✓ migration `AddEarlyTerminationPercentToPricingPolicy` | return date = the planned handover date; rate stored as a percent (2.00 = 2 %), existing rows **0**, no number seeded; rate taken from the policy in force on the move-in date (like rent, rule 4). A handover date in the past is refused. `NoticeDays` stays in the table but is no longer enforced |
| SR-05 | ✓ migration `AddUnitVisit` | staff act only at their own facilities (same scope as tickets and maintenance) |
| SR-06 | **not built — already satisfied** | `BillingWorker` → `BillingCycleService` already raises monthly invoices by schedule, idempotently, with logging. A second job as written would double-bill and count `GraceDays` twice |
| SR-07 | ✓ `docs/rental-lifecycle.md` | — |
| §8 | ✓ `CLAUDE.md` | — |

Frontend updated in the same go (routes, labels, cancel check-in, unit visits, early-return penalty). Open
questions in §6 are untouched. Setting `EarlyTerminationPercent` today needs `POST /api/pricing-policies`
(the admin page copies terms from the active policy and cannot edit them).

---

## 1. The real finding: "handover" means the opposite of what the code says

The review minutes say *"Handover kh được"*. That is not primarily a design complaint — it is a
vocabulary collision.

`CLAUDE.md` glossary today: *"Handover — the physical check-in where staff gives the customer the
unit and lock."* The **start** of the rental.

The team and the instructor use "handover" to mean **giving the unit back** at the end — *"I am done
using this storage, I cancel my monthly subscription and I am ready to move my stuff out."*

Every other item in the minutes follows from this. "Move out" looked like a duplicate of handover,
hence *"xoá move out"*. The check-in/check-out questions looked unanswered because the word was
already taken by the wrong step.

**The fix is to re-point the vocabulary, not to redesign the lifecycle.** The flow itself —
reservation → contract → signature → keys handed over → monthly billing → unit returned → inspection
→ deposit settled — is correct and stays.

---

## 2. The agreed vocabulary

| Moment in the lifecycle | Called today | Called from now on |
|---|---|---|
| Staff meets customer, gives lock and card, unit → `Occupied`, contract → `Active` | `Handover` | **`CheckIn`** |
| Customer enters and leaves the unit they rent, during the rental | *(nothing)* | **`UnitVisit`** |
| Customer gives the unit back, cancels the subscription | `MoveOutRequest` | **`Handover`** |
| Staff inspects the returned unit, decides the refund | `ReturnInspection` | **`HandoverInspection`** |

Both renames go **all the way down** — entity, table, configuration, DTOs, service, routes, UI
labels. Confirmed.

> **"Check-in" is deliberately used for two things**: the start of a rental (`CheckIn`) and a customer
> visiting their unit (`UnitVisit.CheckInAt`). This was a conscious decision — it matches how the team
> and the instructor speak. Put a note in the glossary so nobody "fixes" it later.

---

## 3. Migration order — do not get this wrong

The name `Handover` is **vacated by one entity and claimed by another**. Two tables cannot hold the
name at once, so this is two migrations in a fixed order. Doing it in one step, or in the other
order, fails.

```
Step 1   Handover        → CheckIn              migration: RenameHandoverToCheckIn
Step 2   MoveOutRequest  → Handover             migration: RenameMoveOutToHandover
         ReturnInspection→ HandoverInspection
```

Run and verify step 1 completely — build, `database update`, confirm the `CheckIn` table exists and
`Handover` does not — **before** starting step 2.

`Contract.HandoverId` and any FK named after the old handover must follow step 1, not step 2.

---

## 4. Backend tasks

Ordered by dependency. Each one builds and is testable before the next.

### `SR-01` · Rename Handover → CheckIn (start of rental)

- Entity `Handover` → `CheckIn`, table `Handover` → `CheckIn`, PK `HandoverId` → `CheckInId`
- `HandoverConfiguration` → `CheckInConfiguration`
- `Features/Handovers/` → `Features/CheckIns/`, `IHandoverService` → `ICheckInService`
- `HandoversController` → `CheckInsController`, route `/api/handovers` → `/api/check-ins`
- `HandoverStatus` enum → `CheckInStatus`, values unchanged
  (`Scheduled`, `InProgress`, `Completed`, `Cancelled`)
- Migration `RenameHandoverToCheckIn`
- Business rule 3 wording changes: **check-in activates the contract**, not signing. The behaviour
  does not change.

### `SR-02` · Complete the check-in endpoints — *item 2.1*

Today only `POST /api/handovers` exists. The code even carries the comment *"No GET endpoint for a
handover yet"*. Staff cannot see or manage what they created. Add:

| Method | Route | Roles |
|---|---|---|
| `GET` | `/api/check-ins?status=&page=&pageSize=` | `FacilityStaff,FacilityManager` |
| `GET` | `/api/check-ins/{id}` | `FacilityStaff,FacilityManager` |
| `POST` | `/api/check-ins/{id}/cancel` | `FacilityStaff,FacilityManager` |

`POST /api/check-ins` then returns `201` **with** a `Location` header pointing at the new `GET`.

Cancel releases the held unit back to `Available` and returns the reservation to `Confirmed`.

### `SR-03` · Rename MoveOut → Handover (end of rental)

Only after `SR-01` is applied and verified.

- `MoveOutRequest` → `Handover`, `MoveOutRequestId` → `HandoverId`
- `ReturnInspection` → `HandoverInspection`, `ReturnInspectionId` → `HandoverInspectionId`
- `MoveOutRequestStatus` → `HandoverStatus`, values unchanged
  (`Requested`, `Approved`, `Inspected`, `Completed`, `Cancelled`)
- `Features/MoveOuts/` → `Features/Handovers/`, `IMoveOutService` → `IHandoverService`
- `MoveOutRequestsController` → `HandoversController`, route `/api/move-out-requests` →
  `/api/handovers`
- Sub-routes keep their shape: `/{id}/approve`, `/{id}/inspection`, `/{id}/settle-deposit`
- Migration `RenameMoveOutToHandover`

Note the two services both end up called `IHandoverService` at different times. After `SR-03`,
`IHandoverService` means **the end of the rental**. The start is `ICheckInService`.

### `SR-04` · Allow early return, with a penalty

Customer may now give the unit back **before** the contract `EndDate`.

- **Remove the notice-period validation.** Business rule 8 as written no longer applies — a customer
  does not have to give notice. Delete the check in the request service.
- Penalty on early return: **a percentage of the deposit.**
  `Penalty = DepositAmount × EarlyTerminationPercent`
- Store the rate as `PricingPolicy.EarlyTerminationPercent`, `decimal(5,2)`, NOT NULL, alongside
  `LateFeePercentPerWeek`. Migration `AddEarlyTerminationPercentToPricingPolicy`.
- Apply it at deposit settlement only when the return date is before `Contract.EndDate`. A return on
  or after `EndDate` carries no penalty.
- Surface it in `HandoverInspection` as part of `OtherDeduction`, and show it as its own line in the
  settlement response so the customer can see what was taken and why.

> **The percentage value itself is not decided** — see Open questions. Do not seed a number.

### `SR-05` · Unit visits — customer check-in / check-out

New entity. Staff record the visit manually at reception; this is **not** derived from
`AccessCode` or `AccessLog`, which stay exactly as they are.

```
UnitVisit
  UnitVisitId    int        PK
  ContractId     int        FK → Contract.ContractId
  StorageUnitId  int        FK → StorageUnit.StorageUnitId
  RecordedBy     int        FK → User.UserId          (the staff member)
  CheckInAt      datetime2  NOT NULL
  CheckOutAt     datetime2  NULL                      (null = still inside)
  Note           nvarchar(200) NULL
```

Index `ContractId`, `StorageUnitId`, `CheckInAt`. Migration `AddUnitVisit`.

| Method | Route | Roles |
|---|---|---|
| `POST` | `/api/unit-visits/check-in` | `FacilityStaff,FacilityManager` |
| `POST` | `/api/unit-visits/{id}/check-out` | `FacilityStaff,FacilityManager` |
| `GET` | `/api/unit-visits?contractId=&page=&pageSize=` | staff: all · customer: own only |

Rules: refuse check-in with `409` unless the contract is `Active`. Refuse a second open visit on the
same contract while one has no `CheckOutAt`. Refuse check-out on a visit already closed.

### `SR-06` · Monthly invoice generation

Business rule 5 stands: invoices are raised per billing cycle by a scheduled job, never on demand.

- `InvoiceGenerationService : BackgroundService` in `StorEase.Infrastructure/Services`
- Runs once daily. For every `Active` contract whose `PaymentDay` matches today and which has no
  invoice for the current cycle, raise one `Invoice` + `InvoiceItem` for `MonthlyRent`
- `IssueDate` = today, `DueDate` = `IssueDate` + `PricingPolicy.GraceDays`, status `Issued`
- **Idempotent** — running twice in one day raises nothing the second time. Check before inserting
- Register in `Program.cs` with `AddHostedService`
- Log each run: contracts examined, invoices raised

### `SR-07` · End-to-end flow write-up

The instructor asked how registration, payment, contract, monthly bill, check-in and check-out fit
together. Write `docs/rental-lifecycle.md` covering, in order:

1. Customer registers and signs in
2. Searches a facility, reserves a unit type → unit is held, `Reserved`
3. Deposit invoice raised and paid
4. Staff assigns a specific unit
5. Contract drafted → customer e-signs
6. **Check-in**: staff hands over lock and card → contract `Active`, unit `Occupied`, access code issued
7. During the rental: `UnitVisit` records each visit; monthly invoice raised automatically by the
   billing job on `PaymentDay`
8. **Handover**: customer requests to give the unit back, early or at term
9. Staff inspects → `HandoverInspection` records condition and deductions
10. Deposit settled, minus damage, outstanding invoices, and the early-termination penalty if any
11. Unit returns to `Available`

State for each step which table changes and which status it moves to. This is a document, not code —
but it is the deliverable the instructor actually asked for, so do not skip it.

---

## 5. Not backend — forward to the frontend team

Both of these are **already built on the backend**. Verified in the repo on 14 Oct. Nothing to do
here; the gap is the admin UI.

| Item | Backend endpoint that already exists | Owner |
|---|---|---|
| **2.2** Set price per unit type for the business manager | `PUT /api/pricing-policies/{id}/unit-prices/{unitTypeId}` · `[Authorize(Roles = BusinessOperationsManager)]` | Thiện (admin side) |
| **2.3** Assign staff directly to a facility | `PUT /api/staff/{id}/facilities` · `[Authorize(Roles = SystemAdministrator)]` | Thiện (admin side) |
| *"Fix giao diện của handover"* | `POST /api/check-ins` after `SR-01`; list and detail arrive in `SR-02` | Thiện (admin side) |

Tell Thiện the route names change in `SR-01` and `SR-03`. He must not start these screens until the
renames are merged, or he will write them twice.

---

## 6. Open questions — do not implement

Confirm these with the instructor before any code is written for them.

1. **The early-termination percentage.** `SR-04` adds the column; nobody has agreed the number.
   Do not seed a value and do not invent one.
2. **Item 2.4 in the minutes is empty** — the number is there, the text is not. Ask what was said.
3. **Who may assign staff to a facility.** Today it is `SystemAdministrator` only. The minutes say
   *"phân nhân viên trực tiếp cho facilities"* without naming a role. If the facility manager is
   meant to do this, the role list on `PUT /api/staff/{id}/facilities` changes.
4. **Whether anything else about check-in is wrong.** The rejection in the minutes is explained by
   the vocabulary collision, and `SR-01`–`SR-02` address it. If the instructor meant something
   further, it has not been captured here.

---

## 7. Order of work

```
SR-01  rename Handover → CheckIn          migration 1
SR-02  complete the check-in endpoints
SR-03  rename MoveOut → Handover          migration 2   ← only after SR-01 is verified
SR-04  early return + penalty             migration 3
SR-05  unit visits                        migration 4
SR-06  monthly billing job                no migration
SR-07  lifecycle write-up                 no code
```

`SR-01` and `SR-03` touch files owned by both Khang and Nhân. Agree a window, do them in one sitting,
merge immediately. Do not leave a half-applied rename on a branch overnight.

---

## 8. `CLAUDE.md` edits required

These are now wrong in `CLAUDE.md` and must be corrected in the same PR as the code:

- **Glossary** — `Handover` now means the end of the rental. Add `Check-in` for the start, `Unit
  visit` for a customer visit, and a line saying "check-in" is used for both the rental start and a
  visit.
- **Rule 3** — "Handover activates the contract" → "**Check-in** activates the contract".
- **Rule 8** — move-out notice period no longer applies. Replace with: *early return is allowed; a
  penalty of `DepositAmount × EarlyTerminationPercent` applies when the return date falls before the
  contract's `EndDate`*.
- **Section 11 schema** — rename `Handover` → `CheckIn`, `MoveOutRequest` → `Handover`,
  `ReturnInspection` → `HandoverInspection`; add `UnitVisit`; add
  `PricingPolicy.EarlyTerminationPercent`.
- **Section 12 relationships** — same renames; add `Contract → UnitVisit`,
  `StorageUnit → UnitVisit`, `User → UnitVisit` (via `RecordedBy`).
- **Section 3 layout** — `Features/MoveOuts` → `Features/Handovers`, and add `Features/CheckIns`,
  `Features/UnitVisits`.

---

## 9. Definition of done

Per task, all of:

1. `dotnet build` and `dotnet test` clean from `BE/`
2. The migration applies to an empty database, and `dotnet ef migrations list` shows no `(Pending)`
3. No reference to the old name survives — `git grep -i moveout`, `git grep -i returninspection`,
   and after `SR-01`, no `Handover` that means the start of a rental
4. Every new endpoint carries `[Authorize(Roles = …)]` or an explicit `[AllowAnonymous]`
5. Expected failures return `409` / `404` / `403`, never `500`
6. `CLAUDE.md` updated per section 8 in the same PR
7. The route list regenerated and the frontend told what changed
