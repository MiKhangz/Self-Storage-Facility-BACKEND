# DB_REVIEW_FIXES — changes to make before the PR

Answers to the six review points raised after `InitialSchema` was generated, plus one item that
was missed. Apply all of these, regenerate the migration, then open the PR.

Nothing here has been shared yet, so regenerating is free. Do not defer any of it to a later
migration.

---

## 0. First — prove the database exists

The report said the `InitialSchema` migration was created, but did not show that
`dotnet ef database update` ran. Run the five checks in `DB_SETUP.md` section 6 and paste the
results before doing anything else:

1. Table count — 31 expected (30 plus `__EFMigrationsHistory`)
2. `Contract`'s columns with their SQL types
3. `Contract.Status` length — `varchar(20)`
4. Foreign key count — 59
5. `dotnet build` and `dotnet test` from `BE/`, both clean

**If `MonthlyRent` or `DepositAmount` reads `decimal(18,2)`**, the configuration is wrong.
Fix the `IEntityTypeConfiguration`, drop the database, regenerate the migration. Never hand-edit
a generated migration file.

---

## 1. Nullable columns — accepted as decided

The rule applied was right and needs no change:

- Columns for events that may not have happened yet are nullable — `SignedAt`, `PaidAt`,
  `SentAt`, `ResolvedAt`, `CompletedAt`, `EffectiveTo`, `SuspendedAt`, `AccessSuspendedAt`,
  `InspectionSlot`, `CheckInSlot`
- Optional detail is nullable — `CitizenId`, `DateOfBirth`, `Address`, `Zone`, `Note`, `Phone`,
  `OpeningHours`, `LockSerial`, `CardNumber`, `ConditionNote`, `ReferenceNo`, `RefundMethod`,
  `RefundAccount`, `Reason`, `EntityName`, `EntityId`, `IpAddress`, `DeviceId`, `Description`,
  `Floor.Name`, `Cost`
- Everything else `NOT NULL`

---

## 2. `Payment.ReceivedBy` → nullable

Correct catch. An online gateway payment has no staff member receiving it. The schema in
`CLAUDE.md` section 11 was wrong.

- Make `Payment.ReceivedBy` nullable in the entity and the configuration
- Update `CLAUDE.md` section 11 to mark it *(nullable)*
- Update section 14's relationship row for `User → Payment` to *optional*

---

## 3. Contract status in business rule 1

`Contract` has no `Pending` status. The rule means **`Active` or `Draft`** — a `Draft` contract
already claims its unit.

Update `CLAUDE.md` section 6, rule 1, to read:

> **A unit can carry only one active contract at a time.** Before creating a contract or holding
> a unit, check for an existing contract on that unit whose status is `Active` or `Draft`.

---

## 4. Notice period — add the column now

Business rule 8 needs a notice period and no column stores one. Add it to `PricingPolicy`,
where the other policy numbers live:

```
PricingPolicy.NoticeDays   int   NOT NULL   default 30
```

Add the row to `CLAUDE.md` section 11 under `PricingPolicy`, after `GraceDays`.

---

## 5. Status values for the remaining string columns

These had no list, so they were left as plain strings. Make them enums, stored as strings,
`varchar(20)`, same as every other status column.

| Column | Values |
|---|---|
| `Shift.ShiftType` | `Morning`, `Afternoon`, `Night` |
| `StaffTask.TaskType` | `Handover`, `Inspection`, `Maintenance`, `Reminder`, `Other` |
| `Payment.Method` | `Cash`, `BankTransfer`, `Gateway`, `Card` |
| `Reminder.Channel` | `Email`, `SMS` |
| `SupportTicket.Priority` | `Low`, `Normal`, `High`, `Urgent` |
| `TicketCategory.DefaultPriority` | `Low`, `Normal`, `High`, `Urgent` |
| `ReturnInspection.ConditionResult` | `Good`, `MinorDamage`, `MajorDamage` |
| `MoveOutRequest.RefundMethod` | `BankTransfer`, `Cash` |
| `AccessLog.Result` | `Granted`, `Denied`, `Expired` |

`Reminder.Template` stays a plain string — it names a message template and is open-ended.

`SupportTicket.Priority` and `TicketCategory.DefaultPriority` share one `Priority` enum.

---

## 6. Navigation collections

One-way navigation is correct for the database but awkward in LINQ. Add collection properties on
the parent side for the five aggregates that will be queried together. This is a code-only
change and produces no migration.

```
Facility        → Floors, StorageUnits
Contract        → Invoices, AccessCodes, Renewals
Invoice         → InvoiceItems, Payments
SupportTicket   → TicketMessages
PricingPolicy   → UnitPrices, FeeTypes
```

Do not add collections anywhere else. `User` in particular should stay collection-free — it is
referenced by sixteen tables and the navigation properties would be noise.

---

## 7. Unique indexes — add them

The schema does not ask for these, but each one prevents a real bug.

| Index | Why |
|---|---|
| `User.Email` unique | Login identifies a user by email |
| `StorageUnit (FacilityId, UnitCode)` unique composite | Two units with the same code in one facility is a bug; the same code in different facilities is fine |
| `Contract.ContractNumber` unique | Printed on the contract |
| `Invoice.InvoiceNumber` unique | Printed on the invoice |
| `Reservation.ReservationCode` unique | The customer quotes this at check-in |
| `SupportTicket.TicketCode` unique | Quoted in support conversations |
| `Payment.PaymentCode` unique | Receipt reference |

---

## 8. Then regenerate

Items 2, 4, 5 and 7 all change the schema. Drop the database, delete the migration, generate one
fresh migration — do not stack a second migration on top.

```
dotnet ef database drop -f  -p StorEase.Infrastructure -s StorEase.Api
dotnet ef migrations remove -p StorEase.Infrastructure -s StorEase.Api
dotnet ef migrations add InitialSchema -p StorEase.Infrastructure -s StorEase.Api
dotnet ef database update              -p StorEase.Infrastructure -s StorEase.Api
```

Then run section 0's five checks again. Foreign key count stays 59; index count rises by 7
unique indexes.

---

## 9. What to commit

Do **not** run `git add .`.

Commit:

- `BE/` — entities, enums, `AppDbContext`, configurations, the migration, `Program.cs`
- `CLAUDE.md` with the edits from items 2, 3 and 4
- `DB_SETUP.md`, `DB_REVIEW_FIXES.md`
- `docs/*.drawio` — the ERDs and context diagrams, which `CLAUDE.md` section 9 points at

Do not commit:

- `appsettings.Development.json` — already gitignored, keep it that way
- Office lock files. Add `~$*` to `.gitignore` first.

```
git checkout -b db-initial-schema
git add BE CLAUDE.md DB_SETUP.md DB_REVIEW_FIXES.md docs .gitignore
git commit -m "feat(DB): initial EF Core schema, 30 tables"
git push -u origin db-initial-schema
```

Then open the PR so Nhân and Trường can read the schema before they start their modules.
