# CLAUDE.md

Guidance for Claude Code working in the **backend** repository of this project.

---

## 1. What this project is

**Self-Storage Facility Rental and Management System** — SWP391 capstone, Topic 5, FA26 (FPT University).

Customers rent storage units online: they search a facility, reserve a unit, pay a deposit, sign a
contract, receive the keys and an access code at check-in, are billed monthly, can renew, and finally
move out with a return inspection and deposit refund. Staff run the facility side: assigning units,
performing handover, chasing overdue invoices, handling support tickets and maintenance.

Reference design: `https://mystorage.vn/vi/` for the customer side. **The brand in this project is
"StorEase"** — an invented name. Never use MyStorage branding, logos or copy anywhere.

### Actors

| Actor | Does |
|---|---|
| **Storage Customer** | Searches, reserves, pays, signs, accesses the unit, renews, moves out, raises tickets |
| **Facility Staff** | Assigns units, runs check-in/handover, records payments, inspects on return, works tickets |
| **Facility Manager** | Approves assignments and renewals, manages units and staff shifts, sees reports |
| **Business Operations Manager** | Sets pricing policies and fees, approves refunds, reads revenue reports |
| **System Administrator** | Manages facilities, staff accounts, roles, and reads the audit log |

### The seven flows

1. Account & access management
2. Facility and storage unit management
3. Booking and reservation
4. Check-in and handover
5. Contract, access code and renewal
6. Billing, payment and overdue handling
7. Move-out, return inspection and support

---

## 2. Stack

This repository is the **backend only**. The frontend lives in its own repository
(`Self-Storage-Facility-FRONTEND`) with its own `CLAUDE.md`. Never add frontend code here.

| Layer | Choice |
|---|---|
| Runtime | .NET 8, ASP.NET Core Web API, C# 12 |
| ORM | EF Core 8, **code-first with migrations** |
| Database | SQL Server 2022 (LocalDB in dev) |
| Auth | **JWT bearer**, `role` claim; the frontend holds the token in memory |
| Testing | xUnit + FluentAssertions |

The frontend consumes this API from a different origin, so **CORS must allow the Vite dev server
at `http://localhost:5173`** in development.

---

## 3. Repository layout

```
D:\Self-Storage-Facility-BACKEND\
└── BE/
    ├── StorEase.sln
    ├── .gitignore                      # also ignores appsettings.Development.json and .env
    ├── StorEase.Domain/                # references nothing
    │   ├── Entities/
    │   └── Enums/
    ├── StorEase.Application/           # → Domain          | FluentValidation
    │   ├── Common/                     # Result<T>, PagedResult<T>, exceptions
    │   ├── Interfaces/                 # IUnitOfWork, IRepository<T>, IJwtService, IEmailService…
    │   └── Features/                   # one folder per feature, each with Dtos/ and Validators/
    │       └── Auth, Users, Facilities, StorageUnits, Reservations, Handovers,
    │           Contracts, AccessCodes, Renewals, Pricing, Invoices, Payments,
    │           OverdueCases, MoveOuts, SupportTickets, Maintenance, Staff,
    │           Reports, ActivityLogs
    ├── StorEase.Infrastructure/        # → Application, Domain | EF Core SqlServer, Tools, Jwt
    │   ├── Persistence/
    │   │   ├── AppDbContext.cs
    │   │   ├── Configurations/         # one IEntityTypeConfiguration<T> per entity
    │   │   ├── Repositories/
    │   │   └── Seed/
    │   ├── Migrations/
    │   └── Services/                   # JwtService, EmailService, PaymentGatewayClient
    ├── StorEase.Api/                   # → all three, but only calls Application
    │   ├── Controllers/
    │   ├── Middleware/
    │   └── appsettings.json
    └── tests/                          # "tests" solution folder
        ├── StorEase.Application.Tests/ # → Application, Domain | xUnit, FluentAssertions
        └── StorEase.Api.Tests/         # → Api | xUnit, FluentAssertions, Mvc.Testing
```

**Dependency direction is one-way and must never be violated:**

```
Api  →  Application  →  Domain
         ↑
   Infrastructure
```

`Domain` references nothing. `Application` references `Domain` only. `Infrastructure` references
`Application` and `Domain`. `Api` references all three but only ever calls into `Application`.

---

## 4. Commands

Everything runs from the `BE` folder, not the repository root.

```bash
cd BE
dotnet restore
dotnet build
dotnet run --project StorEase.Api                 # https://localhost:7001
dotnet test

# migrations — always from BE/
dotnet ef migrations add <Name> -p StorEase.Infrastructure -s StorEase.Api
dotnet ef database update      -p StorEase.Infrastructure -s StorEase.Api
dotnet ef migrations remove    -p StorEase.Infrastructure -s StorEase.Api
```

---

## 5. Backend rules

### Entities and EF configuration

- Entities live in `StorEase.Domain/Entities`, are plain C# classes, have **no data annotations**
  and **no EF attributes**. All mapping goes in `IEntityTypeConfiguration<T>` classes under
  `Infrastructure/Persistence/Configurations`, one file per entity.
- Primary keys are `int` identity, named `<Entity>Id` — `ContractId`, `InvoiceId`. The one exception
  is `ActivityLog.LogId` and `AccessLog.AccessLogId`, which are `bigint`.
- **Every money column is `decimal(12,2)`.** Configure it explicitly:
  `builder.Property(x => x.MonthlyRent).HasColumnType("decimal(12,2)");`
  Never let EF infer a decimal precision — it silently truncates.
- Status columns are `varchar(20)` in the database, backed by a C# `enum` converted with
  `.HasConversion<string>()`. Store the string, never the int.
- Timestamps are `datetime2`. Store UTC. Convert to Asia/Ho_Chi_Minh only in the UI.
- Delete behaviour: `DeleteBehavior.Restrict` everywhere by default. Nothing in this system is
  hard-deleted — rows get a `Status` of `Cancelled`, `Terminated` or `Inactive`.
- Add an index for every FK and for every column the UI filters on (`Status`, `DueDate`,
  `MoveInDate`, `UnitCode`).

### Services and controllers

- Controllers are thin: validate the model, call one Application service, map the result to an
  HTTP status. **No business logic, no `DbContext`, no LINQ in a controller.**
- Application services return `Result<T>`, never throw for expected failures (unit not available,
  invoice already paid). Exceptions are for bugs only.
- Every service method that writes more than one table runs inside one transaction through
  `IUnitOfWork`.
- DTOs cross the boundary, never entities. A controller must not return a `Domain` type —
  it leaks the whole object graph and causes serialization cycles.
- Validation uses FluentValidation, one validator per request DTO.

### API conventions

- Routes: `/api/{resource}` in kebab-plural — `/api/storage-units`, `/api/move-out-requests`.
- `GET /api/contracts?page=1&pageSize=20&status=Active` returns `PagedResult<T>`.
- Status codes: `200` read, `201` + `Location` create, `204` update/delete, `400` validation,
  `401` no token, `403` wrong role, `404` missing, `409` business-rule conflict.
- Errors return RFC 7807 `ProblemDetails`. One exception-handling middleware produces them; never
  try/catch in a controller to shape an error.
- Authorise by role on every endpoint: `[Authorize(Roles = "FacilityStaff,FacilityManager")]`.
  An endpoint with no `[Authorize]` must have an explicit `[AllowAnonymous]` so the omission is
  visible in review.
- A customer may only read their own reservations, contracts, invoices and tickets. Check the
  `UserId` claim against the row's owner inside the service — not with a query parameter.

---

## 6. Business rules that are easy to get wrong

1. **A unit can carry only one active contract at a time.** Before creating a contract or holding a
   unit, check for an existing contract on that unit whose status is `Active` or `Draft`.
2. **Reservation does not occupy a unit.** Reserving sets the unit to `Reserved`; only a completed
   handover sets it to `Occupied`.
3. **Handover activates the contract**, not signing. Order is: reservation → contract drafted →
   customer e-signs → staff completes handover → contract becomes `Active` → access code issued.
4. **Only one pricing policy is active at a time.** Activating a new version ends the previous one.
   A contract keeps the rent agreed at signing; a later policy change does not alter it.
5. **Invoices are raised per billing cycle for every `Active` contract**, by a scheduled job, not
   on demand.
6. **An overdue case escalates by stage** — reminder → late fee → lockout. Lockout revokes the
   contract's access codes; it does not terminate the contract.
7. **Deposit is refunded only after return inspection**, minus damage and any outstanding invoice.
8. **Move-out requires notice.** Validate the planned move-out date against the contract's notice
   period before accepting the request.
9. **Access codes expire.** Issuing a new one revokes the previous active code for that contract.
10. **Never delete a facility, unit or user that has history.** Deactivate instead.

---

## 7. Reference documents in this repo

Keep these in `/docs`. They are the source of truth; if code and a document disagree, raise it
rather than silently changing either.

| File | What it holds |
|---|---|
| `SWP391_Topic5_Hub_ERD.drawio` | Full physical schema, 30 tables, 57 relationships, Contract at the centre |
| `SWP391_Topic5_Domain_ERD.drawio` | Subject-domain ERD, 15 entities — use this for analysis chapters |
| `SWP391_Topic5_Chen_ERD.drawio` | Chen-notation ERDs, one page per flow |
| `SWP391_Topic5_Entity_ERD_and_States.drawio` | Entity-only ERD + state transition diagrams |
| `SWP391_Topic5_Context_Diagrams.drawio` | Context diagrams (DFD level 0) for Booking & Handover, Contract Lifecycle, Billing & Collection |
| `SWP391_Topic5_Project_Tracking.xlsx` | 71 feature rows with owners, complexity and status |

---

## 8. Team

Five people. The tracking sheet is authoritative; this is the summary. The **In Charge** column of
the tracking sheet uses the short name — `Khang & Đạt` means Khang owns the backend and Đạt the
frontend for that feature.

| Student ID | Name | Short | Role | Area |
|---|---|---|---|---|
| SE171577 | Huỳnh Minh Khang | **Khang** | BE · **Leader** | Accounts & auth, facilities, floors, units, unit status board, reservations, **handover**, staff management, activity log |
| SE172497 | Huỳnh Hữu Trí Nhân | **Nhân** | BE | Contracts, access codes, renewal, move-out & return, lockout, support & maintenance |
| SE184087 | Võ Đan Trường | **Trường** | BE | Pricing policies & fees, quotes, invoices, payments, overdue & reminders, deposit settlement, reports |
| SE172640 | Phạm Lê Thành Đạt | **Đạt** | FE | All customer-side screens (U01–U16), plus reports and staff admin |
| SE185085 | Bùi Đức Thiện | **Thiện** | FE | The rest of the admin side (A01–A21) |

Workload as assigned, counting Simple = 1, Medium = 2, Complex = 3:

| | Khang | Nhân | Trường | Đạt | Thiện |
|---|---|---|---|---|---|
| tasks | 28 | 20 | 22 | 37 | 34 |
| points | 57 | 39 | 50 | 71 | 76 |

---

## 9. Working agreements for Claude Code

- **Read before writing.** Check the existing EF configuration and service for a feature before
  adding to it. If the code and this file disagree, stop and ask — do not silently follow either.
- **Match the schema in section 11 exactly** — table names, column names, types, nullability.
  If a feature needs a column that is not there, say so and propose the migration; do not quietly
  add a property.
- **One feature per change.** Do not refactor unrelated files in the same edit.
- **Write the migration whenever an entity or configuration changes**, with a descriptive name
  (`AddLockoutStageToOverdueCase`), and never edit a migration that has already been applied.
- **Do not scaffold from the database.** This project is code-first.
- Never commit `appsettings.Development.json`, connection strings, JWT signing keys or `.env`.
- Run `dotnet build` and `dotnet test` from `BE/` before declaring a task done.
- Comments explain *why*, not *what*. No commented-out code.
- Answer in English in code and comments; UI copy is Vietnamese.

---

## 10. Glossary

| Term | Means |
|---|---|
| **Reservation** | A customer's intent to rent, holding a unit until move-in day |
| **Contract** | The rental agreement; the hub of the whole system |
| **Handover** | The physical check-in where staff gives the customer the unit and lock |
| **Access code** | Time-limited gate/unit code tied to an active contract |
| **Overdue case** | An escalation record attached to an unpaid invoice |
| **Return inspection** | Condition check at move-out that decides the deposit refund |
| **Unit type** | Size class of a unit (area, dimensions) — the thing that is priced |
| **Pricing policy** | A dated, versioned set of unit prices and fees |

---

## 11. Database schema

30 tables, 59 foreign keys. SQL Server types. `FK → Table.Column` marks a foreign key;
*(nullable)* means the relationship is optional (zero-or-one on that side).

### Accounts & staff

**`Role`**

| Column | Type | Key |
|---|---|---|
| `RoleId` | `int` | PK |
| `RoleName` | `varchar(50)` |  |
| `Description` | `varchar(200)` |  |

**`User`**

| Column | Type | Key |
|---|---|---|
| `UserId` | `int` | PK |
| `RoleId` | `int` | FK → `Role.RoleId` |
| `FullName` | `nvarchar(100)` |  |
| `Email` | `varchar(100)` |  |
| `PhoneNumber` | `varchar(20)` |  |
| `PasswordHash` | `varchar(255)` |  |
| `CitizenId` | `varchar(20)` |  |
| `DateOfBirth` | `date` |  |
| `Address` | `nvarchar(200)` |  |
| `Status` | `varchar(20)` |  |
| `CreatedAt` | `datetime2` |  |

**`StaffFacility`**

| Column | Type | Key |
|---|---|---|
| `StaffFacilityId` | `int` | PK |
| `UserId` | `int` | FK → `User.UserId` |
| `FacilityId` | `int` | FK → `Facility.FacilityId` |
| `AssignedDate` | `date` |  |

**`Shift`**

| Column | Type | Key |
|---|---|---|
| `ShiftId` | `int` | PK |
| `UserId` | `int` | FK → `User.UserId` |
| `FacilityId` | `int` | FK → `Facility.FacilityId` |
| `ShiftDate` | `date` |  |
| `StartTime` | `time` |  |
| `EndTime` | `time` |  |
| `ShiftType` | `varchar(20)` |  |

**`StaffTask`**

| Column | Type | Key |
|---|---|---|
| `TaskId` | `int` | PK |
| `ShiftId` | `int` | FK → `Shift.ShiftId` *(nullable)* |
| `AssignedTo` | `int` | FK → `User.UserId` |
| `TaskType` | `varchar(20)` |  |
| `ReferenceCode` | `varchar(30)` |  |
| `ScheduledAt` | `datetime2` |  |
| `Status` | `varchar(20)` |  |

**`ActivityLog`**

| Column | Type | Key |
|---|---|---|
| `LogId` | `bigint` | PK |
| `UserId` | `int` | FK → `User.UserId` |
| `FacilityId` | `int` | FK → `Facility.FacilityId` *(nullable)* |
| `Action` | `varchar(50)` |  |
| `EntityName` | `varchar(50)` |  |
| `EntityId` | `varchar(30)` |  |
| `IpAddress` | `varchar(45)` |  |
| `Result` | `varchar(20)` |  |
| `CreatedAt` | `datetime2` |  |


### Facilities & units

**`Facility`**

| Column | Type | Key |
|---|---|---|
| `FacilityId` | `int` | PK |
| `ManagerId` | `int` | FK → `User.UserId` *(nullable)* |
| `Code` | `varchar(10)` |  |
| `Name` | `nvarchar(100)` |  |
| `Address` | `nvarchar(200)` |  |
| `District` | `nvarchar(50)` |  |
| `Phone` | `varchar(20)` |  |
| `OpeningHours` | `varchar(50)` |  |
| `Status` | `varchar(20)` |  |

**`Floor`**

| Column | Type | Key |
|---|---|---|
| `FloorId` | `int` | PK |
| `FacilityId` | `int` | FK → `Facility.FacilityId` |
| `FloorNumber` | `int` |  |
| `Name` | `nvarchar(50)` |  |

**`UnitType`**

| Column | Type | Key |
|---|---|---|
| `UnitTypeId` | `int` | PK |
| `Code` | `varchar(10)` |  |
| `Name` | `nvarchar(50)` |  |
| `AreaM2` | `decimal(5,2)` |  |
| `Width` | `decimal(4,2)` |  |
| `Depth` | `decimal(4,2)` |  |
| `Height` | `decimal(4,2)` |  |
| `IsClimateControlled` | `bit` |  |

**`StorageUnit`**

| Column | Type | Key |
|---|---|---|
| `StorageUnitId` | `int` | PK |
| `FacilityId` | `int` | FK → `Facility.FacilityId` |
| `FloorId` | `int` | FK → `Floor.FloorId` |
| `UnitTypeId` | `int` | FK → `UnitType.UnitTypeId` |
| `UnitCode` | `varchar(20)` |  |
| `Zone` | `varchar(10)` |  |
| `Status` | `varchar(20)` |  |
| `CurrentPrice` | `decimal(12,2)` |  |
| `Note` | `nvarchar(200)` |  |

**`MaintenanceOrder`**

| Column | Type | Key |
|---|---|---|
| `MaintenanceOrderId` | `int` | PK |
| `StorageUnitId` | `int` | FK → `StorageUnit.StorageUnitId` |
| `TicketId` | `int` | FK → `SupportTicket.TicketId` *(nullable)* |
| `AssignedTo` | `int` | FK → `User.UserId` *(nullable)* |
| `Description` | `nvarchar(300)` |  |
| `ScheduledAt` | `datetime2` |  |
| `CompletedAt` | `datetime2` |  |
| `Cost` | `decimal(12,2)` |  |
| `Status` | `varchar(20)` |  |


### Pricing & fees

**`PricingPolicy`**

| Column | Type | Key |
|---|---|---|
| `PricingPolicyId` | `int` | PK |
| `CreatedBy` | `int` | FK → `User.UserId` |
| `Version` | `int` |  |
| `EffectiveFrom` | `date` |  |
| `EffectiveTo` | `date` |  |
| `DepositMonths` | `int` |  |
| `GraceDays` | `int` |  |
| `NoticeDays` | `int` |  |
| `LateFeePercentPerWeek` | `decimal(5,2)` |  |
| `SuspendAfterDays` | `int` |  |
| `ClearOutAfterDays` | `int` |  |
| `CancelFreeHours` | `int` |  |
| `Status` | `varchar(20)` |  |

**`UnitPrice`**

| Column | Type | Key |
|---|---|---|
| `UnitPriceId` | `int` | PK |
| `PricingPolicyId` | `int` | FK → `PricingPolicy.PricingPolicyId` |
| `UnitTypeId` | `int` | FK → `UnitType.UnitTypeId` |
| `BasePricePerMonth` | `decimal(12,2)` |  |
| `Discount3Month` | `decimal(5,2)` |  |
| `Discount6Month` | `decimal(5,2)` |  |
| `Discount12Month` | `decimal(5,2)` |  |

**`FeeType`**

| Column | Type | Key |
|---|---|---|
| `FeeTypeId` | `int` | PK |
| `PricingPolicyId` | `int` | FK → `PricingPolicy.PricingPolicyId` |
| `Code` | `varchar(20)` |  |
| `Name` | `nvarchar(50)` |  |
| `Amount` | `decimal(12,2)` |  |
| `IsPercentage` | `bit` |  |


### Booking & handover

**`Reservation`**

| Column | Type | Key |
|---|---|---|
| `ReservationId` | `int` | PK |
| `CustomerId` | `int` | FK → `User.UserId` |
| `FacilityId` | `int` | FK → `Facility.FacilityId` |
| `UnitTypeId` | `int` | FK → `UnitType.UnitTypeId` |
| `StorageUnitId` | `int` | FK → `StorageUnit.StorageUnitId` *(nullable)* |
| `CreatedBy` | `int` | FK → `User.UserId` *(nullable)* |
| `ReservationCode` | `varchar(20)` |  |
| `MoveInDate` | `date` |  |
| `RentalMonths` | `int` |  |
| `CheckInSlot` | `datetime2` |  |
| `Status` | `varchar(20)` |  |
| `CreatedAt` | `datetime2` |  |

**`Handover`**

| Column | Type | Key |
|---|---|---|
| `HandoverId` | `int` | PK |
| `ReservationId` | `int` | FK → `Reservation.ReservationId` |
| `ContractId` | `int` | FK → `Contract.ContractId` *(nullable)* |
| `StorageUnitId` | `int` | FK → `StorageUnit.StorageUnitId` |
| `StaffId` | `int` | FK → `User.UserId` |
| `HandoverTime` | `datetime2` |  |
| `LockSerial` | `varchar(30)` |  |
| `CardNumber` | `varchar(30)` |  |
| `ConditionNote` | `nvarchar(300)` |  |
| `Status` | `varchar(20)` |  |


### Contract & access

**`Contract`**

| Column | Type | Key |
|---|---|---|
| `ContractId` | `int` | PK |
| `ReservationId` | `int` | FK → `Reservation.ReservationId` |
| `CustomerId` | `int` | FK → `User.UserId` |
| `StorageUnitId` | `int` | FK → `StorageUnit.StorageUnitId` |
| `ContractNumber` | `varchar(20)` |  |
| `StartDate` | `date` |  |
| `EndDate` | `date` |  |
| `MonthlyRent` | `decimal(12,2)` |  |
| `DepositAmount` | `decimal(12,2)` |  |
| `PaymentDay` | `int` |  |
| `SignedAt` | `datetime2` |  |
| `Status` | `varchar(20)` |  |

**`AccessCode`**

| Column | Type | Key |
|---|---|---|
| `AccessCodeId` | `int` | PK |
| `ContractId` | `int` | FK → `Contract.ContractId` |
| `CodeHash` | `varchar(255)` |  |
| `IssuedAt` | `datetime2` |  |
| `ExpiresAt` | `datetime2` |  |
| `IsActive` | `bit` |  |
| `SuspendedAt` | `datetime2` |  |

**`AccessLog`**

| Column | Type | Key |
|---|---|---|
| `AccessLogId` | `bigint` | PK |
| `AccessCodeId` | `int` | FK → `AccessCode.AccessCodeId` |
| `StorageUnitId` | `int` | FK → `StorageUnit.StorageUnitId` |
| `AccessTime` | `datetime2` |  |
| `Result` | `varchar(20)` |  |
| `DeviceId` | `varchar(30)` |  |

**`Renewal`**

| Column | Type | Key |
|---|---|---|
| `RenewalId` | `int` | PK |
| `ContractId` | `int` | FK → `Contract.ContractId` |
| `ApprovedBy` | `int` | FK → `User.UserId` *(nullable)* |
| `RequestedAt` | `datetime2` |  |
| `ExtendMonths` | `int` |  |
| `NewEndDate` | `date` |  |
| `NewMonthlyRent` | `decimal(12,2)` |  |
| `Status` | `varchar(20)` |  |


### Move-out & return

**`MoveOutRequest`**

| Column | Type | Key |
|---|---|---|
| `MoveOutRequestId` | `int` | PK |
| `ContractId` | `int` | FK → `Contract.ContractId` |
| `RequestedAt` | `datetime2` |  |
| `PlannedMoveOutDate` | `date` |  |
| `Reason` | `nvarchar(200)` |  |
| `InspectionSlot` | `datetime2` |  |
| `RefundMethod` | `varchar(20)` |  |
| `RefundAccount` | `varchar(50)` |  |
| `Status` | `varchar(20)` |  |

**`ReturnInspection`**

| Column | Type | Key |
|---|---|---|
| `ReturnInspectionId` | `int` | PK |
| `MoveOutRequestId` | `int` | FK → `MoveOutRequest.MoveOutRequestId` |
| `StorageUnitId` | `int` | FK → `StorageUnit.StorageUnitId` |
| `StaffId` | `int` | FK → `User.UserId` |
| `InspectedAt` | `datetime2` |  |
| `ConditionResult` | `varchar(20)` |  |
| `CleaningFee` | `decimal(12,2)` |  |
| `RepairCharge` | `decimal(12,2)` |  |
| `OtherDeduction` | `decimal(12,2)` |  |
| `RefundAmount` | `decimal(12,2)` |  |
| `Status` | `varchar(20)` |  |


### Billing & overdue

**`Invoice`**

| Column | Type | Key |
|---|---|---|
| `InvoiceId` | `int` | PK |
| `ContractId` | `int` | FK → `Contract.ContractId` *(nullable)* |
| `ReservationId` | `int` | FK → `Reservation.ReservationId` *(nullable)* |
| `CustomerId` | `int` | FK → `User.UserId` |
| `InvoiceNumber` | `varchar(20)` |  |
| `IssueDate` | `date` |  |
| `DueDate` | `date` |  |
| `TotalAmount` | `decimal(12,2)` |  |
| `PaidAmount` | `decimal(12,2)` |  |
| `Status` | `varchar(20)` |  |

**`InvoiceItem`**

| Column | Type | Key |
|---|---|---|
| `InvoiceItemId` | `int` | PK |
| `InvoiceId` | `int` | FK → `Invoice.InvoiceId` |
| `FeeTypeId` | `int` | FK → `FeeType.FeeTypeId` *(nullable)* |
| `Description` | `nvarchar(200)` |  |
| `Quantity` | `int` |  |
| `UnitAmount` | `decimal(12,2)` |  |
| `LineAmount` | `decimal(12,2)` |  |

**`Payment`**

| Column | Type | Key |
|---|---|---|
| `PaymentId` | `int` | PK |
| `InvoiceId` | `int` | FK → `Invoice.InvoiceId` |
| `ReceivedBy` | `int` | FK → `User.UserId` *(nullable)* |
| `PaymentCode` | `varchar(20)` |  |
| `Method` | `varchar(20)` |  |
| `Amount` | `decimal(12,2)` |  |
| `PaidAt` | `datetime2` |  |
| `ReferenceNo` | `varchar(50)` |  |
| `Status` | `varchar(20)` |  |

**`OverdueCase`**

| Column | Type | Key |
|---|---|---|
| `OverdueCaseId` | `int` | PK |
| `ContractId` | `int` | FK → `Contract.ContractId` |
| `InvoiceId` | `int` | FK → `Invoice.InvoiceId` *(nullable)* |
| `OverdueSince` | `date` |  |
| `DaysOverdue` | `int` |  |
| `LateFeeAmount` | `decimal(12,2)` |  |
| `Stage` | `varchar(30)` |  |
| `AccessSuspendedAt` | `datetime2` |  |
| `ResolvedAt` | `datetime2` |  |

**`Reminder`**

| Column | Type | Key |
|---|---|---|
| `ReminderId` | `int` | PK |
| `InvoiceId` | `int` | FK → `Invoice.InvoiceId` |
| `Channel` | `varchar(20)` |  |
| `Template` | `varchar(50)` |  |
| `SentAt` | `datetime2` |  |
| `Status` | `varchar(20)` |  |


### Support

**`TicketCategory`**

| Column | Type | Key |
|---|---|---|
| `CategoryId` | `int` | PK |
| `Name` | `nvarchar(50)` |  |
| `DefaultPriority` | `varchar(20)` |  |

**`SupportTicket`**

| Column | Type | Key |
|---|---|---|
| `TicketId` | `int` | PK |
| `CustomerId` | `int` | FK → `User.UserId` |
| `StorageUnitId` | `int` | FK → `StorageUnit.StorageUnitId` *(nullable)* |
| `FacilityId` | `int` | FK → `Facility.FacilityId` |
| `CategoryId` | `int` | FK → `TicketCategory.CategoryId` |
| `AssignedTo` | `int` | FK → `User.UserId` *(nullable)* |
| `TicketCode` | `varchar(20)` |  |
| `Subject` | `nvarchar(200)` |  |
| `Priority` | `varchar(20)` |  |
| `Status` | `varchar(20)` |  |
| `CreatedAt` | `datetime2` |  |
| `ResolvedAt` | `datetime2` |  |

**`TicketMessage`**

| Column | Type | Key |
|---|---|---|
| `MessageId` | `int` | PK |
| `TicketId` | `int` | FK → `SupportTicket.TicketId` |
| `SenderId` | `int` | FK → `User.UserId` |
| `Body` | `nvarchar(1000)` |  |
| `IsInternalNote` | `bit` |  |
| `SentAt` | `datetime2` |  |

---

## 12. Relationship summary

Read this as "one `Parent` has many `Child`". Relationships marked *optional* have a nullable FK.

| Parent | Child | Via | |
|---|---|---|---|
| `Role` | `User` | `User.RoleId` |  |
| `User` | `StaffFacility` | `StaffFacility.UserId` |  |
| `Facility` | `StaffFacility` | `StaffFacility.FacilityId` |  |
| `User` | `Shift` | `Shift.UserId` |  |
| `Facility` | `Shift` | `Shift.FacilityId` |  |
| `Shift` | `StaffTask` | `StaffTask.ShiftId` | *optional* |
| `User` | `StaffTask` | `StaffTask.AssignedTo` |  |
| `User` | `ActivityLog` | `ActivityLog.UserId` |  |
| `Facility` | `ActivityLog` | `ActivityLog.FacilityId` | *optional* |
| `User` | `Facility` | `Facility.ManagerId` | *optional* |
| `Facility` | `Floor` | `Floor.FacilityId` |  |
| `Facility` | `StorageUnit` | `StorageUnit.FacilityId` |  |
| `Floor` | `StorageUnit` | `StorageUnit.FloorId` |  |
| `UnitType` | `StorageUnit` | `StorageUnit.UnitTypeId` |  |
| `StorageUnit` | `MaintenanceOrder` | `MaintenanceOrder.StorageUnitId` |  |
| `SupportTicket` | `MaintenanceOrder` | `MaintenanceOrder.TicketId` | *optional* |
| `User` | `MaintenanceOrder` | `MaintenanceOrder.AssignedTo` | *optional* |
| `User` | `PricingPolicy` | `PricingPolicy.CreatedBy` |  |
| `PricingPolicy` | `UnitPrice` | `UnitPrice.PricingPolicyId` |  |
| `UnitType` | `UnitPrice` | `UnitPrice.UnitTypeId` |  |
| `PricingPolicy` | `FeeType` | `FeeType.PricingPolicyId` |  |
| `User` | `Reservation` | `Reservation.CustomerId` |  |
| `Facility` | `Reservation` | `Reservation.FacilityId` |  |
| `UnitType` | `Reservation` | `Reservation.UnitTypeId` |  |
| `StorageUnit` | `Reservation` | `Reservation.StorageUnitId` | *optional* |
| `User` | `Reservation` | `Reservation.CreatedBy` | *optional* |
| `Reservation` | `Handover` | `Handover.ReservationId` |  |
| `Contract` | `Handover` | `Handover.ContractId` | *optional* |
| `StorageUnit` | `Handover` | `Handover.StorageUnitId` |  |
| `User` | `Handover` | `Handover.StaffId` |  |
| `Reservation` | `Contract` | `Contract.ReservationId` |  |
| `User` | `Contract` | `Contract.CustomerId` |  |
| `StorageUnit` | `Contract` | `Contract.StorageUnitId` |  |
| `Contract` | `AccessCode` | `AccessCode.ContractId` |  |
| `AccessCode` | `AccessLog` | `AccessLog.AccessCodeId` |  |
| `StorageUnit` | `AccessLog` | `AccessLog.StorageUnitId` |  |
| `Contract` | `Renewal` | `Renewal.ContractId` |  |
| `User` | `Renewal` | `Renewal.ApprovedBy` | *optional* |
| `Contract` | `MoveOutRequest` | `MoveOutRequest.ContractId` |  |
| `MoveOutRequest` | `ReturnInspection` | `ReturnInspection.MoveOutRequestId` |  |
| `StorageUnit` | `ReturnInspection` | `ReturnInspection.StorageUnitId` |  |
| `User` | `ReturnInspection` | `ReturnInspection.StaffId` |  |
| `Contract` | `Invoice` | `Invoice.ContractId` | *optional* |
| `Reservation` | `Invoice` | `Invoice.ReservationId` | *optional* |
| `User` | `Invoice` | `Invoice.CustomerId` |  |
| `Invoice` | `InvoiceItem` | `InvoiceItem.InvoiceId` |  |
| `FeeType` | `InvoiceItem` | `InvoiceItem.FeeTypeId` | *optional* |
| `Invoice` | `Payment` | `Payment.InvoiceId` |  |
| `User` | `Payment` | `Payment.ReceivedBy` | *optional* |
| `Contract` | `OverdueCase` | `OverdueCase.ContractId` |  |
| `Invoice` | `OverdueCase` | `OverdueCase.InvoiceId` | *optional* |
| `Invoice` | `Reminder` | `Reminder.InvoiceId` |  |
| `User` | `SupportTicket` | `SupportTicket.CustomerId` |  |
| `StorageUnit` | `SupportTicket` | `SupportTicket.StorageUnitId` | *optional* |
| `Facility` | `SupportTicket` | `SupportTicket.FacilityId` |  |
| `TicketCategory` | `SupportTicket` | `SupportTicket.CategoryId` |  |
| `User` | `SupportTicket` | `SupportTicket.AssignedTo` | *optional* |
| `SupportTicket` | `TicketMessage` | `TicketMessage.TicketId` |  |
| `User` | `TicketMessage` | `TicketMessage.SenderId` |  |
