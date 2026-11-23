# API endpoints

Checked against the code and `docs/openapi.json` on 22 Nov 2026 (161 operations). The exact request and response shapes
are in `docs/openapi.json`; the frontend client in `src/api/generated` is generated from it. This page adds who may
call each route and the rules behind it. Any change is announced in the group chat first.

Swagger (with a working padlock): `https://localhost:7001/swagger`

---

## Renamed 22 Oct 2026 (instructor's vocabulary)

**Handover** starts the rental, **check-in / check-out** is a visit, **return** ends the rental.

| Before | Now |
|---|---|
| `/api/handovers`, `HandoverDto`, `plannedHandoverDate`, `HandoverInspection`, status `PendingHandover`, refund source `Handover` | `/api/returns`, `ReturnDto` (`unitReturnId`), `plannedReturnDate`, `ReturnInspection`, `PendingReturn`, `Return` |
| `/api/check-ins`, `CheckInDto`, `CheckInStatus`, `GET /api/reservations/{id}/check-in`, reservation status `CheckedIn` | `/api/handovers`, `HandoverDto`, `HandoverStatus`, `GET /api/reservations/{id}/handover`, `HandedOver` |
| `/api/unit-visits`, `UnitVisitDto` | `/api/check-ins`, `CheckInDto` (`POST /api/check-ins/check-in`, `/{id}/check-out`) |

This replaces the names of 18 Oct 2026. The left column is history: read every route below with today's meaning.

## Conventions

| Topic | Rule |
|---|---|
| Base URL | `https://localhost:7001` |
| Auth | Login sets an HttpOnly, Secure, SameSite=Strict cookie on `/api`; the web app sends no token of its own and never reads it. `X-Portal: staff` picks the staff cookie, `X-Tab` (16 hex characters) keeps each window's sign-in apart. Swagger and tests may send `Authorization: Bearer <token>` instead; the header wins |
| Sessions | A disabled account, a changed role or a new password ends the session on its next request (`401`) |
| Language | `Accept-Language: vi` or `en` picks the language of error messages |
| JSON | camelCase |
| Enums | sent and returned as **strings**: `"Available"`, never `2` |
| Dates (`date`) | `"2026-10-13"` |
| Timestamps | ISO 8601 **UTC**, ending in `Z`: `"2026-10-11T15:13:30.2045888Z"`. Convert to Asia/Ho_Chi_Minh in the UI |
| Money | JSON number, VND: `900000.00`. Format with `Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' })` |
| Lists | always `PagedResult<T>` (below); `page` from 1, `pageSize` default 20, max 100 |
| CORS | Any origin is allowed in every environment (VS-1341); no credentialed CORS: the web app calls `/api` through its own origin |

### `PagedResult<T>`

```jsonc
{ "items": [ /* T */ ], "page": 1, "pageSize": 20, "totalCount": 6, "totalPages": 1 }
```

### Errors — RFC 7807 ProblemDetails

```jsonc
// 404 / 409 / 403 / 429 — show `title` to the user
{ "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Unit A-103 is Reserved, not Available.", "status": 409, "traceId": "00-…" }

// 400 validation — `errors` is keyed by field (PascalCase)
{ "title": "One or more validation errors occurred.", "status": 400,
  "errors": { "RentalMonths": ["'Rental Months' must be between 1 and 36. You entered 0."] }, "traceId": "00-…" }
```

| Status | Means | Frontend does |
|---|---|---|
| `400` | validation failed | show `errors` next to the fields |
| `401` | no session, or it ended — **empty body**. Login and a bad VNPay signature also answer `401`, with a `title` | go back to login |
| `403` | logged in, wrong role | "not allowed" message |
| `404` | missing, or not yours | not-found state |
| `409` | business rule refused it | show `title` |
| `429` | too many sign-in, register or password attempts; `Retry-After` in seconds | show `title` |

---

## Roles

`StorageCustomer`, `FacilityStaff`, `FacilityManager`, `BusinessOperationsManager`, `SystemAdministrator`
— exactly these strings, in `role` of the login response and the JWT `role` claim.
"Staff" below means `FacilityStaff` or `FacilityManager`.

## Enum values

| Enum | Values |
|---|---|
| `UserStatus` | `Pending`, `Active`, `Disabled` |
| `FacilityStatus` | `Active`, `Inactive` |
| `FloorStatus` | `Open`, `UnderConstruction`, `Renovation`, `Removed` |
| `StorageUnitStatus` | `Available`, `Reserved`, `Occupied`, `Maintenance`, `Retired` |
| `ReservationStatus` | `Pending`, `Confirmed`, `HandedOver`, `Rejected`, `Cancelled`, `Expired` |
| `ContractStatus` | `Draft`, `Active`, `PendingReturn`, `Closed`, `Overdue`, `Suspended`, `Terminated`, `Overstay` |
| `HandoverStatus` | `Scheduled`, `InProgress`, `Completed`, `Cancelled` |
| `CheckOutMethod` | `Desk`, `MarkedByStaff`, `AutoAtClosing` |
| `ReturnStatus` | `Requested`, `Approved`, `Inspected`, `Completed`, `Cancelled` |
| `ReturnInspectionStatus` | `Draft`, `Completed`, `Disputed` |
| `ConditionResult` | `Good`, `MinorDamage`, `MajorDamage` |
| `InvoiceStatus` | `Issued`, `Paid`, `Overdue`, `WrittenOff` |
| `PaymentMethod` | `Cash`, `BankTransfer`, `Gateway`, `Card`, `Deposit` |
| `PaymentStatus` | `Pending`, `Succeeded`, `Failed`, `Refunded` |
| `RefundMethod` | `BankTransfer`, `Cash` |
| `DepositRefundSource` | `Cancellation`, `Return`, `LatePayment` |
| `DepositRefundStatus` | `Pending`, `Paid` |
| `PricingPolicyStatus` | `Draft`, `Active`, `Superseded` |
| `OverdueStage` | `Reminder`, `LateFee`, `Suspended`, `ClearOut`, `Resolved` |
| `ReminderChannel` | `Email`, `SMS` (nothing sends SMS) |
| `ReminderStatus` | `Queued`, `Sent`, `Failed` |
| `Priority` | `Low`, `Normal`, `High`, `Urgent` |
| `SupportTicketStatus` | `New`, `InProgress`, `WaitingForCustomer`, `Resolved`, `Closed` |
| `MaintenanceOrderStatus` | `Open`, `Scheduled`, `InProgress`, `Done`, `Cancelled` |
| `ShiftType` | `Morning`, `Afternoon`, `Night` |
| `TaskType` | `Handover`, `Inspection`, `Maintenance`, `Reminder`, `Other` |
| `StaffTaskStatus` | `ToDo`, `InProgress`, `Done`, `Cancelled` |
| `ActivityResult` | `Success`, `Failure` |

Renewal `status` is text, not an enum: `Requested`, `AwaitingCustomer`, `Approved`, `Rejected`, `Cancelled`.

Unit board colours: `Available` green, `Reserved` amber, `Occupied` red.

---

## The main path, in nine calls

| # | Method | Route | Who | Success |
|---|---|---|---|---|
| 1 | `POST` | `/api/auth/login` | anyone | `200` `LoginResponse` |
| 2 | `GET` | `/api/facilities` | anyone | `200` `PagedResult<FacilityListItemDto>` |
| 3 | `GET` | `/api/storage-units` | anyone | `200` `PagedResult<StorageUnitListItemDto>` |
| 4 | `POST` | `/api/reservations` | `StorageCustomer` | `201` + `Location`, `ReservationDto` |
| 5 | `GET` | `/api/reservations` | any logged-in user | `200` `PagedResult<ReservationDto>` |
| 6 | `POST` | `/api/reservations/{id}/assign-unit` | Staff | `200` `ReservationDto` |
| 7 | `POST` | `/api/contracts` | Staff | `201` + `Location`, `ContractDto` |
| 8 | `POST` | `/api/contracts/{id}/sign` | `StorageCustomer` (own) | `200` `ContractDto` |
| 9 | `POST` | `/api/handovers` | Staff | `201` `HandoverDto` |

Also available:

| Method | Route | Who | Success |
|---|---|---|---|
| `GET` | `/api/reservations/{id}` | any logged-in user (customer: own) | `200` `ReservationDto` |
| `GET` | `/api/contracts/{id}` | any logged-in user, in scope (customer: own) | `200` `ContractDto` |
| `GET` | `/api/contracts?status=&page=&pageSize=` | any logged-in user, in scope (customer: own) | `200` `PagedResult<ContractDto>` |

---

### 1. `POST /api/auth/login`

```jsonc
// request
{ "email": "customer@storease.vn", "password": "Demo@123" }

// 200 LoginResponse
{ "token": "eyJhbGciOi…", "userId": 1, "fullName": "Khách Demo", "role": "StorageCustomer" }
```

`401` wrong email or password (same message for both). `403` account not `Active`. `429` after 20 tries in 5 minutes
for one account from one address, or 120 requests a minute from one address. Also sets the session cookie (see
Conventions); the token in it lives 8 hours.

### 2. `GET /api/facilities?page=1&pageSize=20`

Active facilities only, ordered by name. Visitors and customers see only facilities where staff work; staff see all,
with `staffed`. `search`, `district`, and `includeInactive=true` (admin only) are optional.

```jsonc
// FacilityListItemDto
{ "facilityId": 1, "code": "GV", "name": "StorEase Gò Vấp", "address": "…", "district": "Gò Vấp",
  "phone": null, "openingHours": null, "availableUnits": 6, "staffed": true }
```

### 3. `GET /api/storage-units?facilityId=1&status=Available&page=1&pageSize=20`

All query parameters are optional, also `unitTypeId` and `search` (unit code). Ordered by `unitCode`. `status` is
today's: whether a unit is free for a stay comes from `GET /api/unit-types/{id}/availability`.

```jsonc
// StorageUnitListItemDto
{ "storageUnitId": 3, "unitCode": "A-103", "facilityId": 1, "floorId": 1, "floorName": "Tầng 1",
  "zone": "A", "status": "Available", "currentPrice": 900000.00,
  "unitTypeId": 1, "unitTypeCode": "S5", "unitTypeName": "5 m²", "areaM2": 5.00, "note": null }   // note: staff only
```

### 4. `POST /api/reservations` — customer reserves

```jsonc
// request
{ "facilityId": 1, "unitTypeId": 1, "moveInDate": "2026-10-13", "rentalMonths": 3, "handoverAt": "2026-10-13T09:00:00" }
```

Validation (mirror in Zod): `facilityId > 0`, `unitTypeId > 0`, `moveInDate` not in the past,
`rentalMonths` 1–36. `handoverAt` (optional, Vietnam time) is the handover appointment, checked like
`POST /api/reservations/{id}/appointment`; it counts once the deposit is paid. Left out, the move-in day is the deadline.

`201`, `Location: /api/reservations/{id}`, body `ReservationDto` with `status: "Pending"`. The booking holds the
**expected unit**: the first one, by code, free for every day of the stay. It becomes `Reserved` if it is free today; one
still rented until before move-in follows when its renter leaves. The price version is pinned and the deposit invoice
raised (see below); the customer pays it straight after booking. `404` facility or unit type not found. `409` when no staff work at that facility, or no unit of that
size is free for the whole period.

### 5. `GET /api/reservations?status=Pending&page=1&pageSize=20`

Staff see their facilities' reservations, admin and business operations all; a customer sees only their own.
`status` optional. Newest first.

```jsonc
// ReservationDto — used for the list items and the single reservation
{ "reservationId": 1, "reservationCode": "RS261011806194",
  "customerId": 1, "customerName": "Khách Demo",
  "facilityId": 1, "facilityName": "StorEase Gò Vấp",
  "unitTypeId": 1, "unitTypeName": "5 m²",
  "storageUnitId": 3, "unitCode": "A-103",                       // the expected unit until staff confirm it
  "moveInDate": "2026-10-13", "rentalMonths": 3,
  "status": "Pending", "createdAt": "2026-10-11T15:12:12.2864898Z",
  "contractId": null,
  "depositInvoiceId": 12, "depositAmount": 1800000.00, "depositStatus": "Issued",
  "proposedStorageUnitId": null, "proposedUnitCode": null,       // another unit waiting for the customer's answer
  "handoverSlot": null,                                          // the appointment, UTC
  "unitBlocked": false }                                         // the held unit will not be ready
```

`contractId` is the reservation's latest contract, `null` until staff drafts one. The customer's
"my contract" page and the handover screen use it to find the contract.

### 6. `POST /api/reservations/{id}/assign-unit` — staff assigns a unit

```jsonc
// request — the unit's storageUnitId (an id, not the code; pick it from endpoint 3)
{ "storageUnitId": 3 }
```

`storageUnitId` may be the expected unit or another of the same facility and size.

- The expected unit, or any unit when the booking holds none: `200` with `status: "Confirmed"`; a unit free today
  becomes `Reserved`.
- Another unit: `200`, but the booking is unchanged except `proposedStorageUnitId` / `proposedUnitCode`. The customer
  gets a bell and an email and answers with `accept-unit` or `decline-unit`.

`409` when: the reservation is not waiting for a unit (`Pending`, or `Confirmed` without a unit after an undone
handover) · the unit is of another facility or size · it is in `Maintenance` or `Retired` · it is booked or rented for
part of the stay.

### 7. `POST /api/contracts` — staff drafts the contract

```jsonc
// request
{ "reservationId": 1, "paymentDay": 5 }
```

`201`, `Location: /api/contracts/{id}`, `ContractDto` with `status: "Draft"`, `signedAt: null`.

```jsonc
// ContractDto
{ "contractId": 1, "contractNumber": "CT…", "reservationId": 1,
  "customerId": 1, "customerName": "Khách Demo",
  "storageUnitId": 3, "unitCode": "A-103",
  "startDate": "2026-10-13", "endDate": "2027-01-12",
  "monthlyRent": 900000.00, "depositAmount": 1800000.00, "paymentDay": 5,
  "signedAt": null, "status": "Draft" }
```

Rent and deposit are the ones quoted at booking. `paymentDay` 1–28. `409` unless the reservation is `Confirmed` with
a unit and no contract that has not ended holds that unit.

### 8. `POST /api/contracts/{id}/sign` — customer e-signs

No body. `200` `ContractDto` with `signedAt` set and **status still `Draft`**.

`404` when the contract is not the caller's own (same as missing) · `409` when it is not `Draft` or is already signed. Only `StorageCustomer` may call it (`403` otherwise).
A paper signature is recorded with `POST /api/contracts/{id}/sign-at-desk` (Staff).
`GET /api/contracts/{id}` returns the same `ContractDto`; a customer asking for someone else's contract gets `404`.

### 9. `POST /api/handovers` — staff confirms handover

```jsonc
// request
{ "reservationId": 1, "contractId": 1, "lockSerial": "LK-00291",
  "cardNumber": null, "conditionNote": "Clean, no damage" }
```

Validation: ids `> 0`, `lockSerial` required (max 30), `cardNumber` optional (max 30; keys are handed over, not cards),
`conditionNote` optional (max 300).

```jsonc
// 201 HandoverDto
{ "handoverId": 1, "reservationId": 1, "contractId": 1, "storageUnitId": 3, "unitCode": "A-103",
  "handoverTime": "2026-10-11T15:13:30.2045888Z", "lockSerial": "LK-00291", "cardNumber": null,
  "conditionNote": "Clean, no damage", "status": "Completed", "accessCode": "349122" }
```

In one transaction: contract `Draft` → `Active`, unit `Reserved` → `Occupied`, reservation →
`HandedOver`, access code issued, and the first visit opened. Show `accessCode` large; the customer can also read it
in their account.

`409` when: the deposit is unpaid · the reservation is not `Confirmed` with a unit · the contract belongs to another
reservation or unit · it is not `Draft` · the customer has not signed yet · the unit is not `Reserved` · another
contract that has not ended holds the unit · an early handover would take days promised to another customer. Any `409`
leaves everything unchanged.

### Handover records, and undoing one

| Method | Route | Who | Success |
|---|---|---|---|
| `GET` | `/api/handovers?status=&page=&pageSize=` | Staff (own facilities), `BusinessOperationsManager` (every facility, read only) | `200` `PagedResult<HandoverDetailDto>`: note, photos, who handed over and when; never the access code |
| `POST` | `/api/handovers/{id}/cancel` | Staff | `204` |

`cancel` works only on the day of the handover (Vietnam time), while the contract is `Active` and before any rent is
paid (`409` otherwise). Handover `Cancelled`, contract `Terminated`, codes off, its invoices `WrittenOff`, unit
released, reservation back to `Confirmed` without a unit.

### More contract endpoints

| Method | Route | Who | Success |
|---|---|---|---|
| `GET` | `/api/contracts/export?status=` | Staff | `200` `text/csv` (UTF-8 with BOM), formulas neutralised |
| `POST` | `/api/contracts/{id}/sign-at-desk` | Staff | `200` `ContractDto` with `signedAt` |
| `POST` | `/api/contracts/{id}/access-codes` | Staff | `201` `AccessCodeDto` `{ accessCodeId, contractId, code, issuedAt, expiresAt }` |

There is no `terminate`: a rental ends through a return or a move to the central warehouse, which settle the deposit.

### Check-in and check-out (visits)

| Method | Route | Who | Success |
|---|---|---|---|
| `GET` | `/api/check-ins/board` | Staff | `200` `CheckInBoardRowDto[]`: their rentals, who is inside, who can enter |
| `POST` | `/api/check-ins/find` `{accessCode}` | Staff | `200` the board row; `404` no working code, `409` two renters match |
| `POST` | `/api/check-ins/check-in` `{contractId, note?}` | Staff | `201` `CheckInDto`; `409` without a working code or when already inside |
| `POST` | `/api/check-ins/{id}/check-out` | Staff | `200` `CheckInDto` (`Desk`) |
| `POST` | `/api/check-ins/{id}/mark-left` `{leftAt, reason}` | Staff | `200` `CheckInDto` (`MarkedByStaff`); `leftAt` between coming in and now |
| `GET` | `/api/check-ins?contractId=&customerId=&page=&pageSize=` | any signed-in (customer: own) | `200` `PagedResult<CheckInDto>` |
| `GET` | `/api/check-ins/{id}` | any signed-in (customer: own) | `200` `CheckInDto` |

A code works for one visit: closing a visit issues a new one, unless the code is locked. The hourly job closes forgotten
visits at the facility's closing time (`AutoAtClosing`).

### Access codes and logs

| Method | Route | Who | Success |
|---|---|---|---|
| `POST` | `/api/access-codes/{id}/revoke` | Staff | `200` `AccessCodeSummaryDto` (`isActive: false`) |
| `GET` | `/api/contracts/{id}/access-code` | any signed-in, in scope (customer: own) | `200` `AccessCodeSummaryDto` |
| `POST` | `/api/contracts/{id}/access-codes/request` | `StorageCustomer` (own) | `201` `AccessCodeDto` |
| `GET` | `/api/access-logs?storageUnitId=&customerId=&from=&to=&page=&pageSize=` | `FacilityManager` (own facilities), `BusinessOperationsManager`, `SystemAdministrator` | `200` `PagedResult<AccessLogDto>` |
| `GET` | `/api/access-logs/mine?from=&to=&page=&pageSize=` | `StorageCustomer` | `200` `PagedResult<AccessLogDto>` (own only) |

```jsonc
// AccessCodeSummaryDto — code is filled only for the renter; staff always get null
{ "accessCodeId": 1, "contractId": 1, "issuedAt": "2026-10-11T15:13:30Z", "expiresAt": "2027-01-13T00:00:00Z",
  "isActive": true, "code": "349122" }

// AccessLogDto — newest first
{ "accessLogId": 7, "accessCodeId": 1, "contractId": 1, "customerId": 1, "customerName": "Khách Demo",
  "storageUnitId": 3, "unitCode": "A-103", "accessTime": "2026-10-05T08:00:00Z", "result": "Granted", "deviceId": "GATE-1" }
```

`result`: `Granted`, `Denied`, `Expired`. `from` and `to` are Vietnam days (`2026-10-05`), inclusive, as in every log;
`400` when `from` > `to`. `access-codes` and `access-codes/request`: only for an `Active` contract (`409`), and the
previous code stops. `revoke`: `409` when the code is already inactive. `access-code` (GET): `404` when the contract is
not the caller's or has no working code.

---

## Support tickets (BE-59..62)

### BE-59 · BE-60 — create a ticket, conversation

| Method | Route | Role | Notes |
|---|---|---|---|
| GET | `/api/ticket-categories` | any signed-in | `[{categoryId, name, defaultPriority}]` |
| POST | `/api/support-tickets` | StorageCustomer | `201` + `Location`, body = ticket. |
| GET | `/api/support-tickets/{id}` | any signed-in | Customer: own tickets. FacilityStaff/Manager: tickets of their facilities. Admin/BusinessOps: all. Anyone else's: `404`. |
| GET | `/api/support-tickets/{id}/messages` | any signed-in | Oldest first. Customers never get internal notes. |
| POST | `/api/support-tickets/{id}/messages` | any signed-in | `201`, body = message. |

`POST /api/support-tickets` body: `{categoryId, subject (≤200), description (≤1000), storageUnitId?, facilityId?, priority?}`.
Give `storageUnitId` (needs your own Active/Overdue/Suspended/PendingReturn/Overstay contract on it; the facility comes from the unit) or `facilityId`. `priority` defaults to the category's `defaultPriority` (`Low|Normal|High|Urgent`).
The description is stored as the first message (no description column). Status starts `New`; `ticketCode` = `TK{yyMMdd}{6 digits}`.

`POST .../messages` body: `{body (≤1000), isInternalNote, nextStatus?}`. `isInternalNote` and `nextStatus` (`InProgress|WaitingForCustomer`) are staff-only (`403` for customers).
Status moves: a staff reply to a `New` ticket → `InProgress`; staff `nextStatus` sets it; a customer reply on `WaitingForCustomer` or `Resolved` → `InProgress` (clears `resolvedAt`); an internal note never changes status.
`409` when the ticket is `Closed`. `400` validation, `404` unknown category/unit/facility/ticket.

File attachments (BE-59) are **not built**: there is no table or storage for them.

### BE-61 · BE-62 — queue, assignment, status

| Method | Route | Who | Success |
|---|---|---|---|
| `GET` | `/api/support-tickets?status=&priority=&facilityId=&assignedToMe=false&page=1&pageSize=20` | any logged-in user | `200` `PagedResult<TicketQueueItemDto>` |
| `POST` | `/api/support-tickets/{id}/assign` `{assigneeId, dueDate?}` | `FacilityManager` | `200` `TicketQueueItemDto` |
| `POST` | `/api/support-tickets/{id}/status` `{status}` | customer (own), Staff | `200` `TicketQueueItemDto` |

- List order: `Urgent` > `High` > `Normal` > `Low`, then oldest first. Facility staff and managers see only tickets of their facilities; admin and business manager see all; a customer sees only their own. A ticket outside the caller's scope is `404`.
- `TicketQueueItemDto`: `ticketId, ticketCode, subject, priority, status, categoryId, categoryName, customerId, customerName, facilityId, facilityName, storageUnitId, unitCode, assignedTo, assignedToName, dueDate, createdAt, resolvedAt`.
- Assign: the assignee must be an `Active` `FacilityStaff`/`FacilityManager` with a `StaffFacility` row at the ticket's facility, `dueDate` not in the past; a `New` ticket becomes `InProgress`. `409` for a `Resolved`/`Closed` ticket or an unsuitable assignee; `404` for an unknown assignee.
- Status moves by staff: `New→InProgress`, `InProgress⇄WaitingForCustomer`, any open status `→Resolved` (sets `resolvedAt`). `Closed` only by the owning customer or a `FacilityManager` (`403` otherwise), and is final. Anything else `409`; a customer asking for another status gets `403`.



## Maintenance orders (BE-63)

Base `/api/maintenance-orders`. Write: `FacilityStaff`, `FacilityManager`. Read: those two plus `BusinessOperationsManager`, `SystemAdministrator`. Staff only see orders on units of their own facilities (`StaffFacility` or managed); admin and business manager see all. Anything outside the scope is `404`.

| Method | Route | Notes |
|---|---|---|
| `POST` | `/api/maintenance-orders` | body `{description (≤300), storageUnitId?, ticketId?, scheduledAt?, assignedTo?}`; one of `storageUnitId`/`ticketId` is required. A ticket must name a unit (else `400`) and its unit wins; both given must agree. `assignedTo` must be an active staff member of the unit's facility (`400`). Starts `Open`, or `Scheduled` when `scheduledAt` is given. `201` + `Location`. |
| `GET` | `/api/maintenance-orders?status=&storageUnitId=&page=&pageSize=` | `PagedResult`, newest first. |
| `GET` | `/api/maintenance-orders/{id}` | `{maintenanceOrderId, storageUnitId, unitCode, facilityId, ticketId, assignedTo, description, scheduledAt, completedAt, cost, workNote, status}` |
| `POST` | `/api/maintenance-orders/{id}/status` | body `{status, scheduledAt?, cost?, workNote?}`; returns the order. |

Status moves (anything else `409`): `Open|Scheduled → Scheduled` (needs `scheduledAt`, also reschedules); `Open|Scheduled → InProgress`; `InProgress → Done` (needs `cost ≥ 0` and `workNote ≤500`, sets `completedAt`); `Open|Scheduled|InProgress → Cancelled`. `Done` and `Cancelled` are final.

Unit side effects, in the same transaction: starting work on an `Available` unit makes it `Maintenance` (`Reserved`/`Occupied` units are left alone). On `Done` or `Cancelled`, of work that had started, a unit in `Maintenance` goes back to `Available`, or `Reserved` if a booking holds it,
unless another live (Open/Scheduled/InProgress) order is on it or its floor is closed.


## Staff accounts (BE-64)

`SystemAdministrator`: every route. `FacilityManager`: `GET` and `PUT .../facilities` only, sees `FacilityStaff` accounts and
themselves (others `404`), and may add or remove only the facilities they manage (`403`); the staff member's other
facilities stay. Their own facilities only an admin changes (`403`).
Customers are never returned or editable here (`404`).

| Method | Route | Success |
|---|---|---|
| `GET` | `/api/staff?role=&status=&page=1&pageSize=20` | `200` `PagedResult<StaffAccountDto>` |
| `GET` | `/api/staff/{id}` | `200` `StaffAccountDto` |
| `POST` | `/api/staff` `{fullName, email, phoneNumber, password (8-100), role}` | `201` + `Location` |
| `PUT` | `/api/staff/{id}` `{fullName?, phoneNumber?, role?, status?}` | `200` `StaffAccountDto` |
| `PUT` | `/api/staff/{id}/facilities` `{facilityIds: [int]}` | `200` `StaffAccountDto` |

- `StaffAccountDto`: `userId, fullName, email, phoneNumber, role, status, createdAt, facilities: [{facilityId, facilityName}]`. The password hash is never returned.
- `role` is one of `FacilityStaff|FacilityManager|BusinessOperationsManager|SystemAdministrator` (`400` otherwise; `StorageCustomer` is refused). `404` if that role row is missing.
- Create: new account is `Active`; duplicate email (case-insensitive) is `409`.
- Update: only the fields given change; no delete, lock an account with `status` `Disabled` (not `Pending`). An admin cannot deactivate their own account or change their own role (`409`). A disabled account or a changed role loses its session on the next request.
- Facilities: replaces the whole list. `409` while the person still has shifts ahead, or open tickets, tasks or
  maintenance orders at a facility being removed. Unassigned rows are deleted (`StaffFacility` is a link, not history); unknown facility id is `404` and nothing changes.



## Shifts and tasks (BE-65, BE-66)

### BE-65 — shifts

| Method | Route | Roles | Returns |
|---|---|---|---|
| `POST` | `/api/shifts` `{userId, facilityId, shiftDate, startTime, endTime, shiftType}` | FacilityManager (own facilities), SystemAdministrator | `201` + `Location`, `ShiftDto` |
| `GET` | `/api/shifts?facilityId=&userId=&from=&to=&page=&pageSize=` | `FacilityStaff`, `FacilityManager`, `BusinessOperationsManager`, `SystemAdministrator` | `200` `PagedResult<ShiftDto>`, oldest first |
| `GET` | `/api/shifts/{id}` | `FacilityStaff`, `FacilityManager`, `BusinessOperationsManager`, `SystemAdministrator` | `200` `ShiftDto` |
| `PUT` | `/api/shifts/{id}` `{shiftDate, startTime, endTime, shiftType}` | FacilityManager, SystemAdministrator | `200` `ShiftDto` |
| `DELETE` | `/api/shifts/{id}` | FacilityManager, SystemAdministrator | `204` |
| `GET` | `/api/shifts/timetable?userId=&facilityId=` | FacilityManager (own facilities), SystemAdministrator | `200` `TimetableDto` — the usual week: `slots` `[{day, shiftType}]` (Morning 07–15, Afternoon 15–23), `through` |
| `PUT` | `/api/shifts/timetable` `{userId, facilityId, slots}` | FacilityManager (own facilities), SystemAdministrator | `200` `TimetableDto` with `skipped` (clashes). Rebuilds the person's untouched timetable shifts there for the next 14 days; the hourly job keeps 14 days ahead. A Night slot is `400`. |

```jsonc
// POST body; times are facility-local, "HH:mm:ss"
{ "userId": 4, "facilityId": 1, "shiftDate": "2026-10-20", "startTime": "22:00:00", "endTime": "06:00:00", "shiftType": "Night" }
// 201 ShiftDto
{ "shiftId": 7, "userId": 4, "userName": "Lê Văn B", "facilityId": 1, "facilityName": "Kho Quận 7",
  "shiftDate": "2026-10-20", "startTime": "22:00:00", "endTime": "06:00:00", "endsNextDay": true, "shiftType": "Night" }
```

- `shiftType`: `Morning` | `Afternoon` | `Night`. `endTime` at or before `startTime` means an overnight shift ending the next day (`endsNextDay: true`); equal times are `400`.
- Visibility: FacilityStaff see only their own shifts; FacilityManager see shifts of their facilities; SystemAdministrator and BusinessOperationsManager see all. A shift outside the caller's view is `404`.
- Create: the person must be an `Active` FacilityStaff/FacilityManager with a `StaffFacility` row at that facility (`409` otherwise; `404` for an unknown person); a facility outside a manager's scope is `404`. A shift that starts in the past is `409`.
- No overlap: one person's shifts, at any facility, may not intersect as `[start, end)`; a shift that ends exactly when the next begins is fine. An overnight shift of the previous day counts. `409` with the clashing shift named. `PUT` ignores the shift itself. (Two simultaneous requests can still both pass; accepted for this project.)
- `PUT` cannot change the person or the facility, and refuses a shift that already ended or a move into the past (`409`).
- `DELETE` is a hard delete (a shift is a plan, not history): only before it starts and only with no `StaffTask` on it; otherwise `409`.
- "Now" is Asia/Ho_Chi_Minh (UTC+7).





### BE-66 — tasks in a shift

Base `/api/staff-tasks`. Create: `FacilityManager`. Progress/cancel: `FacilityStaff`, `FacilityManager`. Read: those two plus `BusinessOperationsManager`, `SystemAdministrator`. A staff member sees only their own tasks, a manager the tasks of shifts at their facilities (plus their own), admin and business manager all. Anything outside that view is `404`.

| Method | Route | Notes |
|---|---|---|
| `POST` | `/api/staff-tasks` | body `{shiftId, assignedTo, taskType, referenceCode? (≤30), note? (≤200), scheduledAt}`. The shift must be at one of the manager's facilities (`404`); `assignedTo` must be the person on that shift and `Active` (`400`); `scheduledAt` must fall in `[start, end)` of the shift (`400`). Shift times are facility-local (UTC+7); a shift whose end is not after its start runs past midnight. `taskType` `Other` requires `note` (`400`). Starts `ToDo`. `201` + `Location`. |
| `GET` | `/api/staff-tasks?shiftId=&assignedTo=&status=&page=&pageSize=` | `PagedResult`, ordered by `scheduledAt`. |
| `GET` | `/api/staff-tasks/{id}` | `{taskId, shiftId, facilityId, assignedTo, assignedToName, taskType, referenceCode, note, scheduledAt, status}` |
| `POST` | `/api/staff-tasks/{id}/status` | body `{status}`; returns the task. |

Status moves: `ToDo → InProgress → Done`, only by the assignee (`403` for anyone else, `409` for a skipped step). `Cancelled`: only a `FacilityManager` (`403` otherwise), only from `ToDo`/`InProgress` (`409` once `Done` or `Cancelled`). `Done` and `Cancelled` are final.



---

## Demo accounts

Seeded by Trường's `DbSeeder`. Password for all three: `Demo@123`.

| Email | Role |
|---|---|
| `customer@storease.vn` | `StorageCustomer` |
| `staff@storease.vn` | `FacilityStaff` |
| `admin@storease.vn` | `SystemAdministrator` |

Ids in the examples (`facilityId: 1`, unit A-103 = `storageUnitId: 3`) assume the seeder inserts in
order on an empty database. Read real ids from endpoints 2 and 3, never hard-code them.

---

## Return and deposit settlement

| Method | Route | Who | Success |
|---|---|---|---|
| `POST` | `/api/returns` | `StorageCustomer` (own contract) | `201` + `Location`, `ReturnDto` |
| `GET` | `/api/returns?status=Approved&page=1&pageSize=20` | any logged-in user (customer: own) | `200` `PagedResult<ReturnDto>`, nearest `plannedReturnDate` first. Staff queue = `status=Approved` |
| `GET` | `/api/returns/{id}` | any logged-in user (customer: own) | `200` `ReturnDto` (carries `inspection` once inspected) |
| `POST` | `/api/returns/{id}/approve` | Staff | `200` `ReturnDto` |
| `POST` | `/api/returns/{id}/inspection` | Staff | `200` `ReturnDto` |
| `POST` | `/api/returns/{id}/settle-deposit` | `BusinessOperationsManager` | `200` `DepositSettlementDto` |

```jsonc
// POST /api/returns
{ "contractId": 1, "plannedReturnDate": "2026-11-20", "reason": "Relocating",
  "refundMethod": "BankTransfer", "refundAccount": "VCB 0123456789" }   // reason, refund* optional; account required for BankTransfer

// POST …/approve
{ "inspectionSlot": "2026-11-20T02:00:00Z" }

// POST …/inspection
{ "conditionResult": "MinorDamage", "cleaningFee": 100000, "repairCharge": 200000, "otherDeduction": 0 }

// ReturnDto
{ "unitReturnId": 1, "contractId": 1, "contractNumber": "CT…", "customerId": 1, "customerName": "…",
  "storageUnitId": 3, "unitCode": "A-103", "requestedAt": "…Z", "plannedReturnDate": "2026-11-20",
  "reason": null, "inspectionSlot": null, "refundMethod": null, "refundAccount": null,
  "status": "Requested", "moveOutDeadline": "2026-11-20", "accessLockedAt": null,
  "inspection": null /* or { returnInspectionId, inspectedAt, conditionResult, cleaningFee, repairCharge,
                              otherDeduction, refundAmount, status } — refundAmount stays 0 until settled */ }

// DepositSettlementDto
{ "unitReturnId": 1, "depositAmount": 900000, "damageDeductions": 300000, "earlyTerminationPenalty": 0, "outstandingInvoices": 0,
  "refundAmount": 600000, "shortfall": 0, "status": "Completed" }
```

Rules:
- Create: `409` unless the contract is `Active` or `Overstay` and has no open request; `400` if `plannedReturnDate` is in
  the past or more than 15 days away. No notice period (SR-04). Contract becomes `PendingReturn`. For an overstaying
  rental the fee stops there and the suspended codes work again until the deadline. Another customer's contract is `404`.
- `moveOutDeadline` is 15 days after the request, or the end date when the return came from `decline-transfer`. Once it
  has passed without an inspection the hourly job turns the codes off (`accessLockedAt`).
- Approve: only `Requested`; `inspectionSlot` in the future. Inspection: only `Approved`; request becomes `Inspected`.
- Settle: only `Inspected`. When `plannedReturnDate` is before the contract's `endDate`, `earlyTerminationPenalty` =
  deposit × `earlyTerminationPercent` / 100 (the policy the contract was signed under) is deducted and added to the
  inspection's `otherDeduction`.
- `refundAmount = max(0, deposit − cleaning − repair − other − penalty − unpaid Issued/Overdue invoices)`; any excess is
  `shortfall`. The deposit pays those invoices (payment method `Deposit`). Request → `Completed`, contract → `Closed`,
  codes revoked, unit → `Maintenance` after `MajorDamage`, else `Reserved` for the next booking or `Available`. A refund
  becomes a `Pending` deposit refund, which Business Operations pay offline.
- Damage notes and photos are not stored: `ReturnInspection` has no column for them.

---

## Contract renewal

| Method | Route | Who | Success |
|---|---|---|---|
| `POST` | `/api/renewals` | `StorageCustomer` (own contract) | `201` + `Location`, `RenewalDto` |
| `GET` | `/api/renewals?contractId=1&status=Requested&page=1&pageSize=20` | any logged-in user (customer: own) | `200` `PagedResult<RenewalDto>`, newest first |
| `GET` | `/api/renewals/{id}` | any logged-in user (customer: own) | `200` `RenewalDto` |
| `POST` | `/api/renewals/{id}/approve` | `FacilityManager` (own facilities), `BusinessOperationsManager` | `200` `RenewalDto`: `Approved`, or `AwaitingCustomer` with `transferUnitCode` |
| `POST` | `/api/renewals/{id}/reject` | same | `200` `RenewalDto`; contract unchanged. Also withdraws an offer: `409` unless `Requested` or `AwaitingCustomer` |
| `POST` | `/api/renewals/{id}/accept-transfer` | `StorageCustomer` (own) | `200` `RenewalDto`: `Approved`, in the offered unit |
| `POST` | `/api/renewals/{id}/decline-transfer` | `StorageCustomer` (own) | `200` `RenewalDto`: `Rejected`, and a return is created |

```jsonc
// POST /api/renewals
{ "contractId": 1, "extendMonths": 6 }          // extendMonths 1..36

// RenewalDto
{ "renewalId": 1, "contractId": 1, "contractNumber": "CT…", "unitCode": "A-103", "requestedAt": "…Z",
  "extendMonths": 6, "currentEndDate": "2027-01-12", "newEndDate": "2027-07-12",
  "currentMonthlyRent": 1000000, "newMonthlyRent": 900000, "status": "Requested", "approvedBy": null,
  "transferUnitCode": null }
```

Rules:
- Create: `409` unless the contract is `Active` and has no renewal `Requested` or `AwaitingCustomer`. Another
  customer's contract is `404`. The request changes nothing on the contract.
- `newEndDate` continues the months counted from the start date, so a rental from the 31st does not drift.
  `newMonthlyRent` = the active price for the size at that facility less the discount reached (3 / 6 / 12 months); with
  no active price it is the current rent.
- Approve: `409` unless `Requested` and the contract is `Active`; `404` out of scope. If the unit is free for the new
  period the contract's end date and rent and the live code's expiry move. The new rent applies to every month not
  billed yet. The billing job issues the extra months.
- If the next customer booked the unit, approve offers a free unit of the same size instead (`AwaitingCustomer`) and
  holds it. `409` when none is free or the rental has already ended: reject it.
- Accept: the contract moves to that unit at once, keeps its code and is extended; the old unit is released. `409` after
  the end date or when the unit was taken meanwhile.
- Decline: a return on the end date, so no early-return penalty.
- An offer not answered by the end date becomes `Rejected`.

---

## Rental end

A rental ends by a return or, when the renter stays past the end date, by a move to the central warehouse. The days and
the percent come from the pricing policy in force (defaults in brackets).

| Method | Route | Who | Success |
|---|---|---|---|
| `GET` | `/api/contracts/ending?overstayOnly=` | Staff, `BusinessOperationsManager` | `200` `RentalEndDto[]`: ending within `endReminderDays`, or past the end |
| `POST` | `/api/contracts/{id}/end-contact` `{note}` | Staff | `200` `RentalEndDto` with the note of the call |
| `POST` | `/api/contracts/{id}/move-to-central-storage` | `FacilityManager`, `BusinessOperationsManager` | `200` `RentalEndDto` |

- `endReminderDays` (10) before the end, with nothing decided: one bell and email to the renter.
- The day after the end, without an open return or a waiting renewal: contract `Overstay`, codes suspended, and one
  invoice `OVS-C<contractId>` that grows daily: monthly rent / 30 × `overstayFeePercent` (150) / 100 × days.
- The move is allowed `moveOutAfterDays` (7) past the end, or from an overdue case at `ClearOut`; `409` before that or
  with an open return. Contract `Closed`, the deposit pays what is owed and the rest becomes a deposit refund, the unit
  goes to the next booking, and the renter is told the goods are kept `centralStorageKeepDays` (90).

---

## Accounts, facility setup, billing, reports

Full request/response shapes are in `docs/openapi.json` (Swagger). Roles: **C** customer, **S** facility
staff, **M** facility manager, **B** business operations, **A** admin, **anon** no token.

### Accounts (Khang)

| Method & route | Who | Notes |
|---|---|---|
| `POST /api/auth/register` | anon | `201`; account is `Pending` until the emailed link is opened (Development: `Active` at once, no email) |
| `POST /api/auth/verify-email` | anon | `{ token }` → `204` |
| `POST /api/auth/resend-verification` | anon | `204` whether or not the account exists; `429` after 5 in 15 minutes for one account |
| `POST /api/auth/forgot-password` | anon | `204` whether or not the account exists; `429` after 5 in 15 minutes for one email; link valid 30 min, works once |
| `POST /api/auth/reset-password` | anon | `{ token, newPassword }` → `204` |
| `GET/PUT /api/users/me` | any | own profile |
| `POST /api/users/me/password` | any | `{ currentPassword, newPassword }` → `204` and a new session cookie; every other session ends; `429` after 10 in 15 minutes |
| `GET /api/auth/session` | signed in | `{ userId, fullName, role, expiresAt }` from the cookie; `401` without a session |
| `POST /api/auth/logout` | anon | `204`; clears this window's cookie |
| `GET/PUT /api/users/me/notification-settings` | any | `emailReminders`, `smsReminders`, `marketingEmails` |

Login of a `Pending` account answers `403` "verify your email". Register, verify and both password-link routes share
the limit of 120 requests a minute from one address with login.

### Notifications

| Method & route | Who | Notes |
|---|---|---|
| `GET /api/notifications` | C | `{ items, unreadCount }`: the newest 20, each `{ notificationId, title, body, link, createdAt, readAt }` |
| `POST /api/notifications/read` | C | `{ upToId }` → `204`; what arrived later stays unread |

Booking changes, payment reminders and rental-end messages go to the bell, with or without email. Links that sign a
person in or reset a password never do.

### Facilities, floors, units (Khang)

| Method & route | Who |
|---|---|
| `GET /api/facilities?search=&district=` · `GET /api/facilities/{id}` | anon |
| `POST /api/facilities` · `PUT /api/facilities/{id}` | A |
| `POST /api/facilities/{id}/deactivate` · `POST /api/facilities/{id}/activate` | A; deactivate is `409` while units are held or bookings open |
| `GET /api/facilities/{id}/unit-board` | S, M, B, A |
| `GET /api/facilities/{id}/floors` | anon |
| `POST /api/facilities/{id}/floors` · `PUT/DELETE /api/floors/{id}` | M (own facility), A |
| `GET /api/storage-units` · `GET /api/storage-units/{id}` | anon; `note` is filled only for staff |
| `POST /api/storage-units` · `PUT /api/storage-units/{id}` | M (own facility), A |
| `POST /api/storage-units/{id}/retire` · `POST /api/storage-units/{id}/restore` | M (own facility), A |

### Unit sizes, availability, quote

| Method & route | Who | Notes |
|---|---|---|
| `GET /api/unit-types` · `GET /api/unit-types/{id}` | anon | `monthlyPrice` is the lowest at any facility today |
| `GET /api/unit-types/{id}/availability?facilityId=&moveInDate=&rentalMonths=` | anon | units free for every day of that period, and `expectedUnitCode` |
| `GET /api/unit-types/{id}/quote?facilityId=&moveInDate=&rentalMonths=` | anon | `monthlyRent`, `rentSubtotal`, `depositAmount`, `totalAmount` (rent + deposit only; fee types are not billed), `expectedUnitCode`; `409` when none is free |
| `POST /api/unit-types` · `PUT /api/unit-types/{id}` | B | `409` on a duplicate code |
| `DELETE /api/unit-types/{id}` | B | `204`; `409` while units or prices use it |

### Reservations, contract draft, handover (Khang)

| Method & route | Who | Notes |
|---|---|---|
| `GET /api/reservations?status=&search=&facilityId=&moveInFrom=&moveInTo=` | any | customer sees own |
| `POST /api/reservations/walk-in` | S, M | `{ customerId or newCustomer, facilityId, unitTypeId, storageUnitId, moveInDate, rentalMonths }` → `201`: `Confirmed` with that unit, deposit recorded as cash. The unit must be free now and for the whole stay |
| `POST /api/reservations/{id}/change-unit` | S, M | at the desk, `{ storageUnitId }`: an `Available` unit of the same size, free for the stay; a Draft contract moves with its signature cleared |
| `POST /api/reservations/{id}/accept-unit` | C (own) | takes the proposed unit → `Confirmed`; `409` if it was taken meanwhile |
| `POST /api/reservations/{id}/decline-unit` | C (own) | → `Cancelled`, whole deposit owed back |
| `POST /api/reservations/{id}/appointment` | C (own), S, M | changes the appointment picked with the booking: `{ at }` in Vietnam time, from now to the move-in day, 07:00–21:00; `409` before the deposit is paid or once the appointment has passed |
| `POST /api/reservations/{id}/cancel` | C (own), S, M | releases the unit; deposit refundable within `cancelFreeHours`, when staff cancel, or when the unit was not ready |
| `POST /api/reservations/{id}/no-show` | S, M | after the appointment day (the move-in day if none); deposit forfeited; `409` when the unit was not ready |
| `GET /api/reservations/{id}/handover` | S, M | handover checklist |
| `POST /api/contracts` | S, M | `{ reservationId, paymentDay }` (1–28) → Draft contract at the policy rent |
| `GET /api/handovers/{id}` | any (customer own) | with photos |
| `POST /api/handovers/{id}/photos` | S, M | multipart field `file`, JPEG/PNG/WebP ≤ 5 MB |
| `GET /api/handovers/{id}/photos/{photoId}` | any (customer own) | the image |

Creating a reservation also raises its deposit invoice (`DEP<code>`, due when the 1-hour hold ends, or on move-in
day if sooner); the handover answers `409` until it is paid. A booking only holds once its deposit is paid: the
hourly job cancels a booking with none of its deposit paid 1 hour after it was made.

The same job closes missed handovers once the appointment day (the move-in day if none) has passed: `Expired` and the
deposit kept. When StorEase had no unit ready it is no no-show: an early appointment is cleared, and after move-in day
the booking is `Cancelled` with the whole deposit back. A booking starting within `endReminderDays` whose unit is
blocked is flagged (`unitBlocked`) and offered another free unit, which the customer accepts or declines.

### Customers

| Method & route | Who | Notes |
|---|---|---|
| `GET /api/customers?search=&page=&pageSize=` | S, M | name, email or phone; citizen IDs always show only their last four digits |
| `GET /api/customers/{id}` | S, M, B, A | the whole citizen ID for B and A, and for S and M only where the customer booked at their facility |

### Rental terms (public)

| Method & route | Who | Notes |
|---|---|---|
| `GET /api/pricing-policies/current-terms?facilityId=&date=` | anon | The policy in force: `depositMonths`, `graceDays`, `lateFeePercentPerWeek`, `earlyTerminationPercent`, `suspendAfterDays`, `clearOutAfterDays`, `cancelFreeHours`, `endReminderDays`, `overstayFeePercent`, `moveOutAfterDays`, `centralStorageKeepDays`, and `discounts[]` per unit type (3/6/12 months, percent). `404` if no policy is in force. The customer site shows these instead of hard-coded text. |

### Staff facility scope

Reservations, contracts, handovers, returns and check-ins follow the same rule as tickets and maintenance:
FacilityStaff and FacilityManager see and act only on records of facilities they work at (`StaffFacility` or
`Facility.ManagerId`); SystemAdministrator and BusinessOperationsManager see all. Out of scope is `404`.

### Pricing fees (Trường)

Every `/api/pricing-policies` route (list, get, create, edit, unit prices, activate, fees): `BusinessOperationsManager` and
`SystemAdministrator`, except `current-terms`, which is public.

| Method & route | Who | Notes |
|---|---|---|
| `GET /api/pricing-policies` · `GET /api/pricing-policies/{id}` | B, A | every version |
| `POST /api/pricing-policies` | B, A | `201` a Draft with dates and terms; `409` when the dates overlap another policy |
| `PUT /api/pricing-policies/{id}` | B, A | same body as create (`earlyTerminationPercent` 0–100); Draft only, `409` otherwise |
| `PUT /api/pricing-policies/{id}/unit-prices/{unitTypeId}` | B, A | Draft only |
| `POST /api/pricing-policies/{id}/activate` | B, A | on or after its start date, with at least one price; the old version becomes `Superseded` |
| `POST /api/pricing-policies/{id}/fee-types` · `PUT/DELETE /api/fee-types/{id}` | B, A | Draft only |
| `POST /api/pricing-policies/{id}/copy` | B, A | `201` a new Draft (next version, starts today) with that version's prices, discounts, fees and terms; `409` while another Draft exists. How an old price comes back: as a new version, so no past dates change. |
| `DELETE /api/pricing-policies/{id}` | B, A | `204`; removes a Draft with its prices and fees. `409` for an Active or Superseded version (history). |

### A facility's own prices

| Method & route | Who | Notes |
|---|---|---|
| `GET /api/facilities/{id}/price-versions` | B, A, M | the company standard and that facility's versions |
| `POST /api/facilities/{id}/price-versions` | B, A | a Draft; `409` while one exists |
| `PUT/DELETE /api/facilities/{id}/price-versions/{versionId}/unit-prices/{unitTypeId}` | B, A | its own price for a size, or back to the standard; Draft only |
| `POST /api/facilities/{id}/price-versions/{versionId}/publish` | B, A | in force today; the old one becomes `Superseded` |
| `DELETE /api/facilities/{id}/price-versions/{versionId}` | B, A | `204`; Draft only |

### Payments (Trường)

| Method & route | Who | Notes |
|---|---|---|
| `GET /api/invoices?status=&page=&pageSize=` · `GET /api/invoices/{id}` | any | customer sees own; staff their facilities |
| `POST /api/invoices/{id}/payments/online` | C | returns `{ paymentId, paymentCode, amount, status: "Pending", checkoutUrl }`; nothing credited yet |
| `POST /api/payments/{paymentCode}/simulate-success` | C | **Development only**, and only without VNPay keys — stands in for the gateway |
| `GET /api/deposit-refunds?status=Pending&page=&pageSize=` | C (own), M (own facilities), B, A | Deposits owed back: one per refundable cancellation (`source` `Cancellation`), per return settled with a refund (`Return`) and per payment that landed on a closed booking (`LatePayment`). Pending first, oldest first. |
| `POST /api/deposit-refunds/{id}/paid` `{method: Cash\|BankTransfer, referenceNo?}` | B | Marks it `Paid` (time and who). A bank transfer needs `referenceNo` (`400`); already paid `409`. |
| `GET /api/payments/vnpay-ipn?vnp_...` | anon | VNPay's IPN, checked by `vnp_SecureHash` (HMAC-SHA512, `PaymentGateway:VnPayHashSecret`). Always `200` `{ RspCode, Message }`: `00` recorded, `97` bad signature, `01` unknown payment, `04` wrong amount, `02` already settled otherwise |
| `GET /api/payments/vnpay-return?vnp_...` | anon | The frontend forwards VNPay's return parameters; same check, `200` `InvoiceDto` or `401`/`404`/`400`/`409`. Both refuse a message without `vnp_ResponseCode` and `vnp_TransactionStatus` (`400`, IPN `04`) |
| `POST /api/invoices/{id}/payments/manual` | S, M | cash at the counter |
| `GET /api/payments?invoiceId=&status=` | any | customer sees own |
| `POST /api/payments/{id}/refund` | B | `{ reason }`; records money given back offline. `409` for a `Deposit` payment, and for a deposit itself except an overpayment or an undone handover: those go back through Deposit Refunds |
| `POST /api/invoices/{id}/adjustments` | B | `{ description, amount }` (negative = credit) |

### Billing, overdue, reminders (Trường)

A background job runs hourly (idempotent per day): issues each month's rent invoice 7 days before it is due
(`INV<yyyyMM>C<contractId>`), marks unpaid invoices `Overdue`, opens an overdue case after `graceDays`, adds a
late-fee line of `lateFeePercentPerWeek` per full week past grace, locks out at `suspendAfterDays` (access
codes suspended, contract `Suspended`), flags `ClearOut` at `clearOutAfterDays`, and emails reminders 3 days
before due, the day after and weekly once overdue (the bell always; email honours `emailReminders`). Paying in full resolves the case and
restores access. A closed or terminated rental and a closed booking's deposit are not chased.

| Method & route | Who |
|---|---|
| `POST /api/billing/run` | B, A — run the billing part of the job now, for today (not the booking and rental-end parts). `?date=` another day is `400`, except in Development with `Billing:AllowOtherDates` on (E2E only) |
| `GET /api/overdue-cases?stage=&openOnly=true` · `GET /api/overdue-cases/{id}` | S, M, B, A |
| `POST /api/overdue-cases/{id}/lockout` · `POST /api/overdue-cases/{id}/restore-access` | M, B |
| `GET /api/invoices/{id}/reminders` | S, M, B, A |
| `POST /api/invoices/{id}/reminders` | S, M, B — send one now |

### Reports and audit

| Method & route | Who |
|---|---|
| `GET /api/reports/dashboard?facilityId=` | M (own facilities), B, A |
| `GET /api/reports/occupancy?facilityId=` | M, B, A |
| `GET /api/reports/revenue?from=&to=&facilityId=` | M, B, A — defaults to the last 12 months |
| `GET /api/activity-logs?userId=&role=&search=&action=&areas=&signIns=&from=&to=&page=&pageSize=` | A — every write by a signed-in user, plus logins; `from` and `to` are Vietnam days |

Reset the local demo database: `powershell -ExecutionPolicy Bypass -File scripts/reset-demo.ps1`.
