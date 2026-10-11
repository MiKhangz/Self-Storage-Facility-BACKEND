# DEMO_PLAN — core feature demo, 12 Oct

One working day. Read `CLAUDE.md` for the rules that still apply; this file says what we build
today and what we deliberately do not.

---

## 0. Where we actually are

| | Status |
|---|---|
| 30 entities, 30 EF configurations, `InitialSchema` migration | **done**, verified |
| Database created on Khang's machine | done |
| Shared plumbing — `Result<T>`, `PagedResult<T>`, `IUnitOfWork`, repository, exception middleware, `JwtService` | **does not exist** |
| Controllers, services, DTOs, validators | **zero** — `Controllers/` holds a `.gitkeep` |
| Frontend repo | **not scaffolded** |

The good news is that **all 30 entities already exist**, so nobody is blocked waiting for
somebody else's entity. Everyone can write a service against `AppDbContext` from the first hour.

The bad news is the shared plumbing. Three backends cannot write a service until `Result<T>` and
`IUnitOfWork` are on `main`. That is the critical path and it is Khang's first job.

---

## 1. The demo path — one unit, one journey

This is the whole demo. Nine API calls, five screens, one unit moving through its lifecycle.

```
seeded:  facility "StorEase Gò Vấp", 1 floor, 6 units, 2 unit types, 3 accounts

1. Customer logs in                                      → JWT, role StorageCustomer
2. Customer browses Gò Vấp, sees 6 units, 6 Available
3. Customer reserves a 5 m² unit for 3 months            → Reservation Pending
                                                            unit still Available
4. Staff logs in, sees the reservation in the queue
5. Staff assigns unit A-103                              → Reservation Confirmed
                                                            unit A-103 → Reserved
6. Staff drafts the contract from the reservation         → Contract Draft
7. Customer opens it and e-signs                         → SignedAt set, still Draft
8. Staff confirms handover (lock serial, card number)    → Handover Completed
                                                            Contract → Active
                                                            unit A-103 → Occupied
                                                            access code issued
9. Customer sees contract Active and the access code
   Staff unit board shows A-103 Occupied, 5 Available
```

Step 8 is the moment that proves the system. **Handover activates the contract, not signing** —
say that line out loud during the demo, it is the business rule the examiners look for.

### What we are cutting, and say so if asked

No pricing-policy versioning (one seeded policy, price read from `StorageUnit.CurrentPrice`).
No payment gateway (deposit invoice is created and marked paid by a staff button).
No monthly invoicing, no overdue escalation, no lockout, no renewal, no move-out, no support
tickets, no maintenance, no shifts, no reports, no activity log, no email, no forgot-password,
no refresh tokens, no unit tests.

Those are not failures — they are the rest of the sprint. The tracking sheet already has them.

---

## 2. Nine endpoints. Freeze these first.

**Khang writes `docs/api-endpoints.md` with exactly this, in the first 30 minutes, and pushes it
before anyone writes a line of UI.** Field names are frozen after that. Any change goes in the
group chat, not silently.

| # | Method | Route | Roles | Returns |
|---|---|---|---|---|
| 1 | `POST` | `/api/auth/login` | anon | `{ token, userId, fullName, role }` |
| 2 | `GET` | `/api/facilities` | anon | `PagedResult<FacilityListItemDto>` |
| 3 | `GET` | `/api/storage-units?facilityId=1&status=Available&page=1&pageSize=20` | anon | `PagedResult<StorageUnitListItemDto>` |
| 4 | `POST` | `/api/reservations` | `StorageCustomer` | `201` + `Location`, `ReservationDto` |
| 5 | `GET` | `/api/reservations?status=Pending&page=1&pageSize=20` | staff: all · customer: own only | `PagedResult<ReservationListItemDto>` |
| 6 | `POST` | `/api/reservations/{id}/assign-unit` | `FacilityStaff,FacilityManager` | `200` `ReservationDto` |
| 7 | `POST` | `/api/contracts` | `FacilityStaff,FacilityManager` | `201` + `Location`, `ContractDto` |
| 8 | `POST` | `/api/contracts/{id}/sign` | `StorageCustomer` (own) | `200` `ContractDto` |
| 9 | `POST` | `/api/handovers` | `FacilityStaff,FacilityManager` | `201` `HandoverDto` with `accessCode` |

Plus `GET /api/contracts/{id}` and `GET /api/storage-units?facilityId=1` with no status filter for
the unit board. Every list is `PagedResult<T>`. Every error is RFC 7807 `ProblemDetails`.

### Request bodies — frozen

```jsonc
// 1  login
{ "email": "customer@storease.vn", "password": "Demo@123" }

// 4  create reservation
{ "facilityId": 1, "unitTypeId": 2, "moveInDate": "2026-10-13", "rentalMonths": 3 }

// 6  assign unit
{ "storageUnitId": 103 }

// 7  draft contract
{ "reservationId": 1, "paymentDay": 5 }

// 9  confirm handover
{ "reservationId": 1, "contractId": 1, "lockSerial": "LK-00291",
  "cardNumber": "CARD-5512", "conditionNote": "Clean, no damage" }
```

### Three demo accounts, same password `Demo@123`

| Email | Role |
|---|---|
| `customer@storease.vn` | `StorageCustomer` |
| `staff@storease.vn` | `FacilityStaff` |
| `admin@storease.vn` | `SystemAdministrator` |

Seed the hashes, not the plaintext. Put the password on the demo slide so the examiner can log in.

---

## 3. Who does what

Module ownership from the tracking sheet still holds — nobody is working in anyone else's folder.

### Khang — plumbing, auth, units, reservations, handover

| Time | Task | Done when |
|---|---|---|
| 09:00–09:30 | `docs/api-endpoints.md` frozen and pushed | everyone has seen it |
| 09:30–11:00 | **Shared plumbing**, pushed straight to `main` | `Result<T>`, `PagedResult<T>`, `IUnitOfWork`, `Repository<T>`, `ExceptionHandlingMiddleware`, `JwtService`, `AddAuthentication` + `AddAuthorization` in `Program.cs`, Swagger with a bearer button |
| 11:00–12:00 | Endpoint 1, login | a token comes back and Swagger's padlock works |
| 13:00–14:00 | Endpoints 2 and 3, facilities and units | Đạt can list units |
| 14:00–15:30 | Endpoints 4, 5, 6 — reserve and assign | assigning sets the unit to `Reserved` and nothing else |
| 15:30–17:00 | Endpoint 9, handover | contract `Active`, unit `Occupied`, access code row written, all in one `IUnitOfWork` transaction |
| 17:00 | hand the API to Thiện and stay available | — |

**The 11:00 push is a hard deadline.** If the plumbing is not on `main` by then, tell the team and
let them keep using `AppDbContext` directly in a service — ugly, fixable next week, better than
five people idle.

### Nhân — contract draft, sign, activation

| Time | Task |
|---|---|
| 09:30–11:00 | Contract DTOs and `IContractService`; commit the **interface** early so Khang can call `ActivateAsync(contractId, ct)` from handover |
| 11:00–13:00 | Endpoint 7 — draft a contract from a confirmed reservation. Copy `MonthlyRent` from `StorageUnit.CurrentPrice`, `DepositAmount` from the seeded policy's `DepositMonths × rent`, `EndDate` from `MoveInDate + RentalMonths` |
| 14:00–15:00 | Endpoint 8 — e-sign. Sets `SignedAt`. **Does not** change status |
| 15:00–16:00 | `ActivateAsync` — `Draft` → `Active`, issue the access code, revoke any previous one |
| 16:00–17:00 | `GET /api/contracts/{id}`, customer sees only their own — check the `UserId` claim inside the service |

Business rule to enforce in step 2: refuse with `409` if that unit already has a contract whose
status is `Active` or `Draft`.

### Trường — seed data, deposit invoice

| Time | Task |
|---|---|
| 09:30–12:00 | **`DbSeeder`** — this is the most valuable thing anyone builds today. 5 roles, 3 users, 1 facility, 1 floor, 6 units (`A-101`…`A-106`, all `Available`), 2 unit types (5 m² and 10 m²), 1 `Active` pricing policy with `UnitPrice` rows and `DepositMonths = 1`, `NoticeDays = 30`. Idempotent — safe to run twice. Called from `Program.cs` in Development |
| 12:00 | push it. Everyone then has data without touching SQL |
| 13:00–15:00 | Deposit invoice on reservation: `Invoice` + `InvoiceItem`, `Status = Issued`, linked by `ReservationId` |
| 15:00–16:00 | `POST /api/payments` recording a cash payment → invoice `Paid`. `ReceivedBy` = the staff claim |
| 16:00–17:00 | A `scripts/reset-demo.ps1` that drops, updates and reseeds in one command. We will need it tonight |

### Đạt — customer screens

| Time | Task |
|---|---|
| 09:00–10:00 | **Scaffold the frontend repo**: `npm create vite@latest -- --template react-ts`, axios, react-router, Tailwind. Push. Nothing else starts until this exists |
| 10:00–11:00 | Auth context holding the token **in memory only** — no `localStorage`, the assignment is marked on this. Axios interceptor adding the bearer header, 401 → back to login |
| 11:00–12:00 | Login screen. Mock the response until endpoint 1 lands, then swap the base URL |
| 13:00–15:00 | Facility + unit browse: facility card, unit grid with size, price and status chip |
| 15:00–16:00 | Reserve form: unit type, move-in date, months. Form rules mirror the API validator |
| 16:00–17:00 | My reservation / contract page: status, Sign button, and once active the **access code shown large** |

Loading and empty states on every screen. A spinner is enough; a blank screen during the demo
looks broken.

### Thiện — admin screens

| Time | Task |
|---|---|
| 09:00–10:00 | Pull Đạt's scaffold, agree the shared layout and the role guard with him. **Do not build a second auth context** |
| 10:00–12:00 | Reservation queue: table of `Pending` reservations, Assign Unit action with a unit picker |
| 13:00–15:00 | Handover screen: reservation summary, lock serial, card number, condition note, Confirm button, success state showing the issued access code |
| 15:00–17:00 | **Unit status board** — the 6 units as coloured tiles, `Available` green, `Reserved` amber, `Occupied` red. Refresh button. This is the single most convincing screen in the demo; finish it even if the handover form is rough |

---

## 4. Integration checkpoints

Three fixed times. Everyone stops and connects. Fifteen minutes each, no exceptions.

| Time | Prove this |
|---|---|
| **12:30** | A real request crosses from `localhost:5173` to `localhost:7001`, bearer token attached, CORS clean, and login works from the browser. If this fails at 12:30 the whole afternoon is at risk |
| **15:30** | Customer can reserve from the UI and staff sees it in the queue |
| **17:30** | Full nine-step path end-to-end, no manual database edits. Reset with Trường's script and run it again |

### 18:00 — record the backup

Run the path once more with the screen recorder on and narration. Save the video and twelve
screenshots. **If something breaks tomorrow we play this.** Do not skip it; the recording is
twenty minutes of insurance on a day of work.

### 19:00–20:00 — rehearsal

Twice through, with a stopwatch, out loud. Decide who drives and who narrates. Khang narrates the
business rules, Đạt and Thiện drive their own screens.

---

## 5. Rules bent for today, and only today

- **Push to `main` directly** for the shared plumbing. PRs resume tomorrow.
- **No tests.** `dotnet build` clean is the bar today.
- **FluentValidation only on the four `POST` bodies** above. Elsewhere, a null check in the service.
- **Access code is a plain 6-digit string**, hashed with the same hasher as passwords, and returned
  once in the handover response. Real expiry logic is Nhân's next sprint.
- Still non-negotiable: `Result<T>` from services, DTOs across the boundary, `[Authorize]` or an
  explicit `[AllowAnonymous]` on every endpoint, no `DbContext` in a controller, money
  `decimal(12,2)`, the token never in `localStorage`, nothing committed from
  `appsettings.Development.json`.

---

## 6. If something is not ready at 17:30

In order of preference:

1. **Frontend screen missing** → demo that step in **Swagger**. Say "this endpoint is complete,
   the screen lands this week". Swagger with a working padlock looks deliberate, not broken.
2. **Handover transaction failing** → demo steps 1–7 live, then show the unit board after a seeded
   `Occupied` row. Say plainly that activation is mid-implementation.
3. **Nothing connects at all** → play the 18:00 recording and walk the ERD and the state diagram.
   We have strong design artefacts; use them.

Never debug live in front of the examiner. Switch to the fallback within ten seconds.

---

## 7. What to say in the first ninety seconds

> StorEase is a self-storage rental system. Thirty tables, with `Contract` at the centre. Today we
> demo the core rental flow: a customer finds a unit, reserves it, signs, and staff hands it over.
> The rule that drives the whole system is that **handover activates the contract, not signing** —
> the unit is only occupied when the customer is physically holding the key. Watch the unit board
> on the right change as we go.

Then run the nine steps. Twelve minutes, not twenty.
