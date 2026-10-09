# DB_SETUP — build the database with EF Core

Instructions for Claude Code. One-off task, done once by Khang as team leader, then committed.
Read `CLAUDE.md` first — section 11 holds the full schema this file refers to.

---

## 1. Where to work

**`D:\Self-Storage-Facility-BACKEND`**, in the `BE/` folder.

That folder holds the solution, the git remote and `.claude/settings.json`.
`D:\Self-Storage-Facility-Rental-and-Management-System` is a leftover — ignore it.

---

## 2. Scope: the whole schema, one migration

Build **all 30 tables** in a single migration named `InitialSchema`. Not just Khang's modules.

The database is shared by all three backend developers. If Nhân and Trường each generate their
own initial migration, there will be three migrations that each believe they are first, and
merging them is painful. One person creates the schema once; everyone else builds their modules
against a database that already exists.

After this, migrations are additive and per-feature — `AddLockoutStageToOverdueCase` and so on —
and conflicts are rare because each person touches different tables.

---

## 3. Steps

1. **Entities and enums** in `StorEase.Domain` — 30 plain C# classes in `Entities/`, the status
   enums in `Enums/`. No data annotations, no EF attributes.
2. **`AppDbContext`** plus one `IEntityTypeConfiguration<T>` file per entity under
   `StorEase.Infrastructure/Persistence/Configurations/`.
3. **Connection string** in `appsettings.Development.json`, which stays gitignored.
4. **Migration and database**:
   ```
   dotnet ef migrations add InitialSchema -p StorEase.Infrastructure -s StorEase.Api
   dotnet ef database update              -p StorEase.Infrastructure -s StorEase.Api
   ```

### Connection string

**Confirmed on this machine:** SQL Server Express is installed and running as service
`MSSQL$SQLEXPRESS`. LocalDB (`MSSQLLocalDB`) is also available but unused.

Add this to `appsettings.Development.json`, which stays gitignored:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=StorEase;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
  }
}
```

The backslash is **doubled** because JSON treats `\` as an escape character. A single backslash
is the usual cause of "A network-related or instance-specific error occurred while establishing a
connection to SQL Server".

Register it in `Program.cs`:

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.MigrationsAssembly("StorEase.Infrastructure")));
```

`MigrationsAssembly` matters: the `DbContext` lives in `StorEase.Infrastructure` while the startup
project is `StorEase.Api`, and without it EF looks for migrations in the wrong assembly.

Teammates on other machines may have a different instance. Tell them to run
`Get-Service | Where-Object {$_.Name -like "MSSQL*"}` and substitute their own — whatever follows
`MSSQL$` goes after the backslash. `appsettings.Development.json` is per-machine and never
committed, so each person sets their own.

---

## 4. Status values

Every status column is `varchar(20)` in the database, backed by a C# enum converted with
`.HasConversion<string>()`. Store the string, never the int.

### Authoritative — from the state transition diagram, do not change

| Column | Values |
|---|---|
| `StorageUnit.Status` | `Available`, `Reserved`, `Occupied`, `Maintenance`, `Retired` |
| `Reservation.Status` | `Pending`, `Confirmed`, `CheckedIn`, `Rejected`, `Cancelled`, `Expired` |
| `Contract.Status` | `Draft`, `Active`, `PendingMoveOut`, `Closed`, `Overdue`, `Suspended`, `Terminated` |
| `Invoice.Status` | `Issued`, `Paid`, `Overdue`, `WrittenOff` |
| `SupportTicket.Status` | `New`, `InProgress`, `WaitingForCustomer`, `Resolved`, `Closed` |

> The state diagram file says `Rented` for a storage unit. **Use `Occupied`** — it matches
> `CLAUDE.md` section 6 rule 2 and the context diagrams. The diagram will be corrected.

### Drafted from the business rules — use these, flag anything that reads wrong

| Column | Values |
|---|---|
| `User.Status` | `Pending`, `Active`, `Suspended`, `Inactive` |
| `Facility.Status` | `Active`, `Inactive` |
| `PricingPolicy.Status` | `Draft`, `Active`, `Superseded` |
| `Handover.Status` | `Scheduled`, `InProgress`, `Completed`, `Cancelled` |
| `MoveOutRequest.Status` | `Requested`, `Approved`, `Inspected`, `Completed`, `Cancelled` |
| `ReturnInspection.Status` | `Draft`, `Completed`, `Disputed` |
| `Payment.Status` | `Pending`, `Succeeded`, `Failed`, `Refunded` |
| `OverdueCase.Stage` | `Reminder`, `LateFee`, `Suspended`, `ClearOut`, `Resolved` |
| `Reminder.Status` | `Queued`, `Sent`, `Failed` |
| `MaintenanceOrder.Status` | `Open`, `Scheduled`, `InProgress`, `Done`, `Cancelled` |
| `StaffTask.Status` | `ToDo`, `InProgress`, `Done`, `Cancelled` |
| `ActivityLog.Result` | `Success`, `Failure` |

`AccessCode` has no status column — its lifecycle is the `IsActive` bit plus `SuspendedAt`.

---

## 5. Rules for the configuration files

From `CLAUDE.md` section 5, repeated here because they are what goes wrong:

- **Money is `decimal(12,2)`, set explicitly.** EF defaults to `decimal(18,2)` and will not warn.
  ```csharp
  builder.Property(x => x.MonthlyRent).HasColumnType("decimal(12,2)");
  ```
- **Timestamps are `datetime2`**, storing UTC.
- **Enums stored as strings**: `.HasConversion<string>()` with `HasMaxLength(20)`.
- **`DeleteBehavior.Restrict` everywhere.** Nothing is hard-deleted in this system.
- **An index on every foreign key**, and on every column the UI filters by — `Status`, `DueDate`,
  `MoveInDate`, `UnitCode`.
- Primary keys are `int` identity named `<Entity>Id`. The two exceptions are `ActivityLog.LogId`
  and `AccessLog.AccessLogId`, which are `bigint`.

---

## 6. Prove it worked

A migration that applies cleanly can still be wrong. After `database update`, report:

1. **Table count** — should be 31 (30 plus `__EFMigrationsHistory`).
2. **The `Contract` table's columns with their SQL types.** `MonthlyRent` and `DepositAmount`
   must read `decimal(12,2)`, not `decimal(18,2)`. `SignedAt` must be `datetime2`.
3. **One status column's length** — `Contract.Status` should be `varchar(20)`.
4. **Foreign key count** — should be 59.
5. `dotnet build` and `dotnet test` from `BE/`, both clean.

If any of those are wrong, fix the configuration and regenerate the migration. Do **not** edit the
generated migration file by hand.

---

## 7. When it's done

```
git checkout -b db-initial-schema
git add .
git commit -m "feat(DB): initial EF Core schema, 30 tables"
git push -u origin db-initial-schema
```

Then open a PR so Nhân and Trường can read the schema before they start. Once merged, they pull,
run `dotnet ef database update` on their own machines, and begin their modules.

Do not commit `appsettings.Development.json`.
