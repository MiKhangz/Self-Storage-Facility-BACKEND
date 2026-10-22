# API endpoints — demo, 12 Oct

**Frozen.** Field names below match the backend DTOs exactly. Any change is announced in the group
chat first — never silently. Frontend types in `features/<x>/types.ts` mirror these one-to-one.

Swagger (with a working padlock): `https://localhost:7001/swagger`

---

## Renamed 18 Oct 2026 (sprint review, SR-01..SR-05)

The start of a rental is now a **check-in** and **handover** means giving the unit back.

| Before | Now |
|---|---|
| `/api/handovers` (keys handed over), `HandoverDto`, `HandoverStatus` | `/api/check-ins`, `CheckInDto`, `CheckInStatus`; plus `GET /api/check-ins`, `POST /api/check-ins/{id}/cancel` |
| `/api/move-out-requests`, `MoveOutRequestDto`, `plannedMoveOutDate` | `/api/handovers`, `HandoverDto`, `plannedHandoverDate` (sub-routes unchanged) |
| `ReturnInspection` | `HandoverInspection` |
| Contract status `PendingMoveOut` | `PendingHandover` |
| Notice period before moving out | none; an early return keeps `EarlyTerminationPercent` of the deposit (`DepositSettlementDto.earlyTerminationPenalty`) |
| *(none)* | `POST /api/unit-visits/check-in`, `POST /api/unit-visits/{id}/check-out`, `GET /api/unit-visits?contractId=` |

`POST /api/check-ins/{id}/cancel` (Staff): check-in `Cancelled`, contract `Terminated`, codes off, unit `Available`,
reservation back to `Confirmed` without a unit; `assign-unit` accepts such a reservation again.
Unit visits: staff of the contract's facility; `409` unless the contract is `Active`, while a visit is still open,
or on checking out a closed visit. The full lifecycle is in `docs/rental-lifecycle.md`.

## Conventions

| Topic | Rule |
|---|---|
| Base URL | `https://localhost:7001` |
| Auth | `Authorization: Bearer <token>` from endpoint 1. Keep the token **in memory only** |
| JSON | camelCase |
| Enums | sent and returned as **strings**: `"Available"`, never `2` |
| Dates (`date`) | `"2026-10-13"` |
| Timestamps | ISO 8601 **UTC**, ending in `Z`: `"2026-10-11T15:13:30.2045888Z"`. Convert to Asia/Ho_Chi_Minh in the UI |
| Money | JSON number, VND: `900000.00`. Format with `Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' })` |
| Lists | always `PagedResult<T>` (below); `page` from 1, `pageSize` default 20, max 100 |
| CORS | Any origin is allowed in every environment (VS-1341); no credentialed CORS, the JWT goes in the Authorization header |

### `PagedResult<T>`

```jsonc
{ "items": [ /* T */ ], "page": 1, "pageSize": 20, "totalCount": 6, "totalPages": 1 }
```

### Errors — RFC 7807 ProblemDetails

```jsonc
// 404 / 409 / 403 / 501 — show `title` to the user
{ "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Unit A-103 is Reserved, not Available.", "status": 409, "traceId": "00-…" }

// 400 validation — `errors` is keyed by field (PascalCase)
{ "title": "One or more validation errors occurred.", "status": 400,
  "errors": { "RentalMonths": ["'Rental Months' must be between 1 and 36. You entered 0."] }, "traceId": "00-…" }
```

| Status | Means | Frontend does |
|---|---|---|
| `400` | validation failed | show `errors` next to the fields |
| `401` | no token / expired — **empty body** | go back to login |
| `403` | logged in, wrong role | "not allowed" message |
| `404` | missing, or not yours | not-found state |
| `409` | business rule refused it | show `title` |
| `501` | endpoint exists, logic not landed yet | show `title` |

---

## Roles

`StorageCustomer`, `FacilityStaff`, `FacilityManager`, `BusinessOperationsManager`, `SystemAdministrator`
— exactly these strings, in `role` of the login response and the JWT `role` claim.
"Staff" below means `FacilityStaff` or `FacilityManager`.

## Enum values

| Enum | Values |
|---|---|
| `StorageUnitStatus` | `Available`, `Reserved`, `Occupied`, `Maintenance`, `Retired` |
| `ReservationStatus` | `Pending`, `Confirmed`, `CheckedIn`, `Rejected`, `Cancelled`, `Expired` |
| `ContractStatus` | `Draft`, `Active`, `PendingHandover`, `Closed`, `Overdue`, `Suspended`, `Terminated` |
| `CheckInStatus` | `Scheduled`, `InProgress`, `Completed`, `Cancelled` |

Unit board colours: `Available` green, `Reserved` amber, `Occupied` red.

---

## The nine demo endpoints

| # | Method | Route | Who | Success | Owner |
|---|---|---|---|---|---|
| 1 | `POST` | `/api/auth/login` | anyone | `200` `LoginResponse` | Khang |
| 2 | `GET` | `/api/facilities` | anyone | `200` `PagedResult<FacilityListItemDto>` | Khang |
| 3 | `GET` | `/api/storage-units` | anyone | `200` `PagedResult<StorageUnitListItemDto>` | Khang |
| 4 | `POST` | `/api/reservations` | `StorageCustomer` | `201` + `Location`, `ReservationDto` | Khang |
| 5 | `GET` | `/api/reservations` | any logged-in user | `200` `PagedResult<ReservationDto>` | Khang |
| 6 | `POST` | `/api/reservations/{id}/assign-unit` | Staff | `200` `ReservationDto` | Khang |
| 7 | `POST` | `/api/contracts` | Staff | `201` + `Location`, `ContractDto` | Nhân |
| 8 | `POST` | `/api/contracts/{id}/sign` | `StorageCustomer` (own) | `200` `ContractDto` | Nhân |
| 9 | `POST` | `/api/check-ins` | Staff | `201` `CheckInDto` | Khang |

Also available:

| Method | Route | Who | Success | Owner |
|---|---|---|---|---|
| `GET` | `/api/reservations/{id}` | any logged-in user (customer: own) | `200` `ReservationDto` | Khang |
| `GET` | `/api/contracts/{id}` | Staff, or the owning customer | `200` `ContractDto` | Nhân |
| `GET` | `/api/contracts?status=&page=&pageSize=` | any logged-in user (customer: own) | `200` `PagedResult<ContractDto>` | Nhân |

---

### 1. `POST /api/auth/login`

```jsonc
// request
{ "email": "customer@storease.vn", "password": "Demo@123" }

// 200 LoginResponse
{ "token": "eyJhbGciOi…", "userId": 1, "fullName": "Khách Demo", "role": "StorageCustomer" }
```

`401` wrong email or password (same message for both). `403` account not `Active`. Token lives 8 hours.

### 2. `GET /api/facilities?page=1&pageSize=20`

Active facilities only, ordered by name.

```jsonc
// FacilityListItemDto
{ "facilityId": 1, "code": "GV", "name": "StorEase Gò Vấp", "address": "…", "district": "Gò Vấp",
  "phone": null, "openingHours": null, "availableUnits": 6 }
```

### 3. `GET /api/storage-units?facilityId=1&status=Available&page=1&pageSize=20`

All query parameters are optional. Omit `status` for the unit board. Ordered by `unitCode`.

```jsonc
// StorageUnitListItemDto
{ "storageUnitId": 3, "unitCode": "A-103", "facilityId": 1, "floorId": 1, "floorName": "Tầng 1",
  "zone": "A", "status": "Available", "currentPrice": 900000.00,
  "unitTypeId": 1, "unitTypeCode": "S5", "unitTypeName": "5 m²", "areaM2": 5.00 }
```

### 4. `POST /api/reservations` — customer reserves

```jsonc
// request
{ "facilityId": 1, "unitTypeId": 1, "moveInDate": "2026-10-13", "rentalMonths": 3 }
```

Validation (mirror in Zod): `facilityId > 0`, `unitTypeId > 0`, `moveInDate` not in the past,
`rentalMonths` 1–36.

`201`, `Location: /api/reservations/{id}`, body `ReservationDto` with `status: "Pending"` and
`storageUnitId: null`. **No unit is held yet** — every unit stays `Available`.
`404` facility or unit type not found. `409` no unit of that type is available there.

### 5. `GET /api/reservations?status=Pending&page=1&pageSize=20`

Staff see every reservation; a customer sees only their own. `status` optional. Newest first.

```jsonc
// ReservationDto — used for the list items and the single reservation
{ "reservationId": 1, "reservationCode": "RS261011806194",
  "customerId": 1, "customerName": "Khách Demo",
  "facilityId": 1, "facilityName": "StorEase Gò Vấp",
  "unitTypeId": 1, "unitTypeName": "5 m²",
  "storageUnitId": null, "unitCode": null,
  "moveInDate": "2026-10-13", "rentalMonths": 3,
  "status": "Pending", "createdAt": "2026-10-11T15:12:12.2864898Z",
  "contractId": null }
```

`contractId` is the reservation's latest contract, `null` until staff drafts one. The customer's
"my contract" page and Thiện's check-in screen use it to find the contract.

### 6. `POST /api/reservations/{id}/assign-unit` — staff assigns a unit

```jsonc
// request — the unit's storageUnitId (an id, not the code; pick it from endpoint 3)
{ "storageUnitId": 3 }
```

Unit picker: call endpoint 3 with the reservation's `facilityId` and `status=Available`, then keep
only items whose `unitTypeId` equals the reservation's.

`200` `ReservationDto` with `status: "Confirmed"`, `storageUnitId`, `unitCode`. The unit becomes `Reserved`.
`409` when: the reservation is not `Pending` · the unit is in another facility or of another type ·
the unit is not `Available` · the unit already has an `Active` or `Draft` contract.

### 7. `POST /api/contracts` — staff drafts the contract (Nhân)

```jsonc
// request
{ "reservationId": 1, "paymentDay": 5 }
```

`201`, `Location: /api/contracts/{id}`, `ContractDto` with `status: "Draft"`, `signedAt: null`.

```jsonc
// ContractDto — proposed, Nhân confirms in the group chat
{ "contractId": 1, "contractNumber": "CT…", "reservationId": 1,
  "customerId": 1, "customerName": "Khách Demo",
  "storageUnitId": 3, "unitCode": "A-103",
  "startDate": "2026-10-13", "endDate": "2027-01-13",
  "monthlyRent": 900000.00, "depositAmount": 900000.00, "paymentDay": 5,
  "signedAt": null, "status": "Draft" }
```

### 8. `POST /api/contracts/{id}/sign` — customer e-signs (Nhân)

No body. `200` `ContractDto` with `signedAt` set and **status still `Draft`**.

`404` when the contract is not the caller's own (same as missing) · `409` when it is not `Draft` or is already signed. Only `StorageCustomer` may call it (`403` otherwise).
`GET /api/contracts/{id}` returns the same `ContractDto`; a customer asking for someone else's contract gets `404`.

### 9. `POST /api/check-ins` — staff confirms check-in

```jsonc
// request
{ "reservationId": 1, "contractId": 1, "lockSerial": "LK-00291",
  "cardNumber": "CARD-5512", "conditionNote": "Clean, no damage" }
```

Validation: ids `> 0`, `lockSerial` and `cardNumber` required (max 30), `conditionNote` optional (max 300).

```jsonc
// 201 CheckInDto
{ "checkInId": 1, "reservationId": 1, "contractId": 1, "storageUnitId": 3, "unitCode": "A-103",
  "checkInTime": "2026-10-11T15:13:30.2045888Z", "lockSerial": "LK-00291", "cardNumber": "CARD-5512",
  "conditionNote": "Clean, no damage", "status": "Completed", "accessCode": "349122" }
```

In one transaction: contract `Draft` → `Active`, unit `Reserved` → `Occupied`, reservation →
`CheckedIn`, access code issued. **`accessCode` is returned only here, once** — show it large.

`409` when: the reservation is not `Confirmed` · the contract belongs to another reservation or
unit · the contract is not `Draft` · the customer has not signed yet · the unit is not `Reserved`.
Activation is live: the contract becomes `Active`, the unit `Occupied`, and a 6-digit access code is issued (valid until the day after `endDate`, hash only stored). `409` also when another `Active`/`Draft` contract holds the unit. Any `409` leaves everything unchanged.

### More contract endpoints (Nhân)

| Method | Route | Who | Success |
|---|---|---|---|
| `GET` | `/api/contracts/export?status=` | Staff | `200` `text/csv` (UTF-8 with BOM), same columns as the list |
| `POST` | `/api/contracts/{id}/terminate` | `FacilityManager` | `200` `ContractDto` with `status: "Terminated"` |
| `POST` | `/api/contracts/{id}/access-codes` | Staff | `201` `{ accessCodeId, contractId, code, issuedAt, expiresAt }` |

`terminate`: allowed from `Active`, `Overdue`, `Suspended`, `PendingHandover`; the unit goes back to `Available` and every active access code is revoked. `409` for `Draft`, `Closed`, `Terminated`.
`access-codes`: re-issues a code for an `Active` contract and revokes the previous one (`409` otherwise). `code` is plaintext and shown once.

### Access codes and logs (Nhân, BE-34..38)

| Method | Route | Who | Success |
|---|---|---|---|
| `POST` | `/api/access-codes/{id}/revoke` | Staff | `200` `AccessCodeSummaryDto` (`isActive: false`) |
| `GET` | `/api/contracts/{id}/access-code` | Staff, or the owning customer | `200` `AccessCodeSummaryDto` |
| `POST` | `/api/contracts/{id}/access-codes/request` | `StorageCustomer` (own) | `201` `AccessCodeDto` (plaintext `code`, once) |
| `GET` | `/api/access-logs?storageUnitId=&customerId=&from=&to=&page=&pageSize=` | `FacilityManager` | `200` `PagedResult<AccessLogDto>` |
| `GET` | `/api/access-logs/mine?from=&to=&page=&pageSize=` | `StorageCustomer` | `200` `PagedResult<AccessLogDto>` (own only) |

```jsonc
// AccessCodeSummaryDto — the plaintext is never returned again
{ "accessCodeId": 1, "contractId": 1, "issuedAt": "2026-10-11T15:13:30Z", "expiresAt": "2027-01-14T00:00:00Z", "isActive": true }

// AccessLogDto — newest first
{ "accessLogId": 7, "accessCodeId": 1, "contractId": 1, "customerId": 1, "customerName": "Khách Demo",
  "storageUnitId": 3, "unitCode": "A-103", "accessTime": "2026-10-05T08:00:00Z", "result": "Granted", "deviceId": "GATE-1" }
```

`result`: `Granted`, `Denied`, `Expired`. `from`/`to` are dates (`2026-10-05`), inclusive, UTC; `400` when `from` > `to`.
`revoke`: `409` when the code is already inactive. `access-code` (GET): `404` when the contract is not the caller's or has no valid (active, unexpired) code.
`access-codes/request`: same rules as staff re-issue (`409` unless the contract is `Active`) and revokes the previous code; a customer asking for someone else's contract gets `404`.
`POST /api/contracts/{id}/access-codes` (staff) remains as above.

---

## Support tickets (BE-59..62)

### BE-59 · BE-60 — create a ticket, conversation

| Method | Route | Role | Notes |
|---|---|---|---|
| GET | `/api/ticket-categories` | any signed-in | `[{categoryId, name, defaultPriority}]`. The table has no seed yet. |
| POST | `/api/support-tickets` | StorageCustomer | `201` + `Location`, body = ticket. |
| GET | `/api/support-tickets/{id}` | any signed-in | Customer: own tickets. FacilityStaff/Manager: tickets of their facilities. Admin/BusinessOps: all. Anyone else's: `404`. |
| GET | `/api/support-tickets/{id}/messages` | any signed-in | Oldest first. Customers never get internal notes. |
| POST | `/api/support-tickets/{id}/messages` | any signed-in | `201`, body = message. |

`POST /api/support-tickets` body: `{categoryId, subject (≤200), description (≤1000), storageUnitId?, facilityId?, priority?}`.
Give `storageUnitId` (needs your own Active/Overdue/Suspended/PendingHandover contract on it; the facility comes from the unit) or `facilityId`. `priority` defaults to the category's `defaultPriority` (`Low|Normal|High|Urgent`).
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

Unit side effects, in the same transaction: starting work on an `Available` unit makes it `Maintenance` (`Reserved`/`Occupied` units are left alone). On `Done` or `Cancelled`, a unit in `Maintenance` goes back to `Available` unless another live (Open/Scheduled/InProgress) order is on it.


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
- `role` is one of `FacilityStaff|FacilityManager|BusinessOperationsManager|SystemAdministrator` (`400` otherwise; `StorageCustomer` is refused). `404` if that role row does not exist yet (the `Role` table has no seed).
- Create: new account is `Active`; duplicate email (case-insensitive) is `409`.
- Update: only the fields given change; no delete, lock an account with `status` `Disabled` (not `Pending`). An admin cannot deactivate their own account or change their own role (`409`). A locked account's existing JWT keeps working until it expires.
- Facilities: replaces the whole list. Unassigned rows are deleted (`StaffFacility` is a link, not history); unknown facility id is `404` and nothing changes.



## Shifts and tasks (BE-65, BE-66)

### BE-65 — shifts

| Method | Route | Roles | Returns |
|---|---|---|---|
| `POST` | `/api/shifts` `{userId, facilityId, shiftDate, startTime, endTime, shiftType}` | FacilityManager (own facilities), SystemAdministrator | `201` + `Location`, `ShiftDto` |
| `GET` | `/api/shifts?facilityId=&userId=&from=&to=&page=&pageSize=` | any staff role | `200` `PagedResult<ShiftDto>`, oldest first |
| `GET` | `/api/shifts/{id}` | any staff role | `200` `ShiftDto` |
| `PUT` | `/api/shifts/{id}` `{shiftDate, startTime, endTime, shiftType}` | FacilityManager, SystemAdministrator | `200` `ShiftDto` |
| `DELETE` | `/api/shifts/{id}` | FacilityManager, SystemAdministrator | `204` |

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

## Handover and deposit settlement (Nhân, BE-46..50)

Not part of the frozen demo set. Enum `HandoverStatus`: `Requested`, `Approved`, `Inspected`, `Completed`, `Cancelled`.
`ConditionResult`: `Good`, `MinorDamage`, `MajorDamage`. `RefundMethod`: `BankTransfer`, `Cash`.

| Method | Route | Who | Success |
|---|---|---|---|
| `POST` | `/api/handovers` | `StorageCustomer` (own contract) | `201` + `Location`, `HandoverDto` |
| `GET` | `/api/handovers?status=Approved&page=1&pageSize=20` | any logged-in user (customer: own) | `200` `PagedResult<HandoverDto>`, nearest `plannedHandoverDate` first. Staff queue = `status=Approved` |
| `GET` | `/api/handovers/{id}` | any logged-in user (customer: own) | `200` `HandoverDto` (carries `inspection` once inspected) |
| `POST` | `/api/handovers/{id}/approve` | Staff | `200` `HandoverDto` |
| `POST` | `/api/handovers/{id}/inspection` | Staff | `200` `HandoverDto` |
| `POST` | `/api/handovers/{id}/settle-deposit` | `BusinessOperationsManager` | `200` `DepositSettlementDto` |

```jsonc
// POST /api/handovers
{ "contractId": 1, "plannedHandoverDate": "2026-11-20", "reason": "Relocating",
  "refundMethod": "BankTransfer", "refundAccount": "VCB 0123456789" }   // reason, refund* optional; account required for BankTransfer

// POST …/approve
{ "inspectionSlot": "2026-11-20T02:00:00Z" }

// POST …/inspection
{ "conditionResult": "MinorDamage", "cleaningFee": 100000, "repairCharge": 200000, "otherDeduction": 0 }

// HandoverDto
{ "handoverId": 1, "contractId": 1, "contractNumber": "CT…", "customerId": 1, "customerName": "…",
  "storageUnitId": 3, "unitCode": "A-103", "requestedAt": "…Z", "plannedHandoverDate": "2026-11-20",
  "reason": null, "inspectionSlot": null, "refundMethod": null, "refundAccount": null,
  "status": "Requested",
  "inspection": null /* or { returnInspectionId, inspectedAt, conditionResult, cleaningFee, repairCharge,
                              otherDeduction, refundAmount, status } — refundAmount stays 0 until settled */ }

// DepositSettlementDto
{ "handoverId": 1, "depositAmount": 900000, "damageDeductions": 300000, "earlyTerminationPenalty": 0, "outstandingInvoices": 0,
  "refundAmount": 600000, "shortfall": 0, "status": "Completed" }
```

Rules:
- Create: `409` unless the contract is `Active` and has no open request; `400` if `plannedHandoverDate` is in the past. No notice period (SR-04). Contract becomes `PendingHandover`.
- Settle: when `plannedHandoverDate` is before the contract `EndDate`, `earlyTerminationPenalty` = deposit × `EarlyTerminationPercent` / 100 (policy in force on the move-in date) is deducted and added to the inspection's `otherDeduction`. Another customer's contract is `404`.
- Approve: only `Requested`. Inspection: only `Approved`; request becomes `Inspected`.
- Settle: only `Inspected`. `refundAmount = max(0, deposit − cleaning − repair − other − unpaid Issued/Overdue invoices)`; any excess is `shortfall`. Request → `Completed`, contract → `Closed`, access codes revoked, unit → `Available` (`Maintenance` after `MajorDamage`). **No money is paid out**: the payment gateway is not connected, so the refund is only recorded.
- Damage notes and photos are not stored: `HandoverInspection` has no column for them.

---

## Contract renewal (Nhân, BE-54, BE-55)

Not part of the frozen demo set. `Renewal.Status` values: `Requested`, `Approved`, `Rejected`, `Cancelled` (varchar column, no enum).

| Method | Route | Who | Success |
|---|---|---|---|
| `POST` | `/api/renewals` | `StorageCustomer` (own contract) | `201` + `Location`, `RenewalDto` |
| `GET` | `/api/renewals?contractId=1&status=Requested&page=1&pageSize=20` | any logged-in user (customer: own) | `200` `PagedResult<RenewalDto>`, newest first |
| `GET` | `/api/renewals/{id}` | any logged-in user (customer: own) | `200` `RenewalDto` |
| `POST` | `/api/renewals/{id}/approve` | `FacilityManager` (own facilities), `BusinessOperationsManager` | `200` `RenewalDto`; contract `EndDate`, `MonthlyRent` and live code expiry move; `404` out of scope; `409` unless `Requested` and contract `Active` |
| `POST` | `/api/renewals/{id}/reject` | same | `200` `RenewalDto`; contract unchanged; `409` unless `Requested` |

```jsonc
// POST /api/renewals
{ "contractId": 1, "extendMonths": 6 }          // extendMonths 1..36

// RenewalDto — status + the contract it extends (BE-55)
{ "renewalId": 1, "contractId": 1, "contractNumber": "CT…", "unitCode": "A-103", "requestedAt": "…Z",
  "extendMonths": 6, "currentEndDate": "2027-01-31", "newEndDate": "2027-07-31",
  "currentMonthlyRent": 1000000, "newMonthlyRent": 900000, "status": "Requested", "approvedBy": null }
```

Rules:
- Create: `409` unless the contract is `Active` and has no renewal in `Requested`. Another customer's contract is `404`.
- `newEndDate = contract.endDate + extendMonths`. `newMonthlyRent` = active pricing policy's base price for the unit type less the discount tier reached (≥3 / ≥6 / ≥12 months, percent); if there is no active policy it is the current rent. The contract's own rent and end date are not changed by the request.
- **Not built:** approve/reject (Facility Manager) and applying an approved renewal to the contract; they are not in BE-54/55, so a renewal stays `Requested`.

---

## Added 14 Oct — accounts, facility setup, billing, reports

Full request/response shapes are in `docs/openapi.json` (Swagger). Roles: **C** customer, **S** facility
staff, **M** facility manager, **B** business operations, **A** admin, **anon** no token.

### Accounts (Khang)

| Method & route | Who | Notes |
|---|---|---|
| `POST /api/auth/register` | anon | `201`; account is `Pending` until the emailed link is opened (Development: `Active` at once, no email) |
| `POST /api/auth/verify-email` | anon | `{ token }` → `204` |
| `POST /api/auth/resend-verification` | anon | always `204` |
| `POST /api/auth/forgot-password` | anon | always `204`; link valid 30 min, works once |
| `POST /api/auth/reset-password` | anon | `{ token, newPassword }` → `204` |
| `GET/PUT /api/users/me` | any | own profile |
| `POST /api/users/me/password` | any | `{ currentPassword, newPassword }` |
| `GET/PUT /api/users/me/notification-settings` | any | `emailReminders`, `smsReminders`, `marketingEmails` |

Login of a `Pending` account answers `403` "verify your email".

### Facilities, floors, units (Khang)

| Method & route | Who |
|---|---|
| `GET /api/facilities?search=&district=` · `GET /api/facilities/{id}` | anon |
| `POST /api/facilities` · `PUT /api/facilities/{id}` · `POST /api/facilities/{id}/deactivate|activate` | A |
| `GET /api/facilities/{id}/unit-board` | S, M, B, A |
| `GET/POST /api/facilities/{id}/floors` · `PUT/DELETE /api/floors/{id}` | M (own facility), A |
| `GET /api/storage-units/{id}` · `POST /api/storage-units` · `PUT /api/storage-units/{id}` · `POST .../{id}/retire|restore` | M, A |

### Reservations, contract draft, check-in (Khang)

| Method & route | Who | Notes |
|---|---|---|
| `GET /api/reservations?status=&search=&facilityId=&moveInFrom=&moveInTo=` | any | customer sees own |
| `POST /api/reservations/{id}/cancel` | C (own), S | releases the unit; deposit refundable within `cancelFreeHours` or when staff cancel |
| `POST /api/reservations/{id}/no-show` | S | after move-in date; deposit forfeited |
| `GET /api/reservations/{id}/check-in` | S | check-in checklist |
| `POST /api/contracts` | S | `{ reservationId, paymentDay }` (1–28) → Draft contract at the policy rent |
| `GET /api/check-ins/{id}` | any (customer own) | with photos |
| `POST /api/check-ins/{id}/photos` | S | multipart field `file`, JPEG/PNG/WebP ≤ 5 MB |
| `GET /api/check-ins/{id}/photos/{photoId}` | any (customer own) | the image |

Creating a reservation also raises its deposit invoice (`DEP<code>`, due on move-in day); the check-in
answers `409` until it is paid.

### Rental terms (public)

| Method & route | Who | Notes |
|---|---|---|
| `GET /api/pricing-policies/current-terms` | anon | Policy in force today: `depositMonths`, `graceDays`, `noticeDays`, `lateFeePercentPerWeek`, `suspendAfterDays`, `clearOutAfterDays`, `cancelFreeHours`, and `discounts[]` per unit type (3/6/12 months, percent). `404` if no policy is in force. The customer site shows these instead of hard-coded text. |

### Staff facility scope

Reservations, contracts, check-ins, handovers and unit visits follow the same rule as tickets and maintenance:
FacilityStaff and FacilityManager see and act only on records of facilities they work at (`StaffFacility` or
`Facility.ManagerId`); SystemAdministrator and BusinessOperationsManager see all. Out of scope is `404`.

### Pricing fees (Trường)

Every `/api/pricing-policies` route (list, get, create, edit, unit prices, activate, fees): `BusinessOperationsManager` and
`SystemAdministrator`, except `current-terms`, which is public.

| Method & route | Who |
|---|---|
| `POST /api/pricing-policies/{id}/fee-types` · `PUT/DELETE /api/fee-types/{id}` | B, A (Draft policies only) |
| `PUT /api/pricing-policies/{id}` (same body as create: dates and terms, `earlyTerminationPercent` 0–100) | B, A (Draft policies only, `409` otherwise) |
| `POST /api/pricing-policies/{id}/copy` | B, A | `201` a new Draft (next version, starts today) with that version's prices, discounts, fees and terms; `409` while another Draft exists. How an old price comes back: as a new version, so no past dates change. |
| `DELETE /api/pricing-policies/{id}` | B, A | `204`; removes a Draft with its prices and fees. `409` for an Active or Superseded version (history). |

### Payments (Trường)

| Method & route | Who | Notes |
|---|---|---|
| `POST /api/invoices/{id}/payments/online` | C | returns `{ paymentId, paymentCode, amount, status: "Pending", checkoutUrl }`; nothing credited yet |
| `POST /api/payments/gateway-callback` | anon | `{ paymentCode, status, amount, referenceNo, signature }`, HMAC-SHA256 of `code|status|amount(0.00)|ref` with `PaymentGateway:Secret` |
| `POST /api/payments/{paymentCode}/simulate-success` | C | **Development only** — stands in for the gateway |
| `GET /api/deposit-refunds?status=Pending&page=&pageSize=` | C (own), FM (own facilities), B, A | Deposits owed back: one per refundable cancellation (`source` `Cancellation`) and per handover settled with a refund (`Handover`). Pending first, oldest first. |
| `POST /api/deposit-refunds/{id}/paid` `{method: Cash\|BankTransfer, referenceNo?}` | B | Marks it `Paid` (time and who). A bank transfer needs `referenceNo` (`400`); already paid `409`. |
| `GET /api/payments/vnpay-ipn?vnp_...` | anon | VNPay's IPN, checked by `vnp_SecureHash` (HMAC-SHA512, `PaymentGateway:VnPayHashSecret`). Always `200` `{ RspCode, Message }`: `00` recorded, `97` bad signature, `01` unknown payment, `04` wrong amount, `02` already settled otherwise |
| `GET /api/payments/vnpay-return?vnp_...` | anon | The frontend forwards VNPay's return parameters; same check, `200` `InvoiceDto` or `401`/`404`/`400`/`409` |
| `POST /api/invoices/{id}/payments/manual` | S, M | cash at the counter |
| `GET /api/payments?invoiceId=&status=` | any | customer sees own |
| `POST /api/payments/{id}/refund` | B | `{ reason }` |
| `POST /api/invoices/{id}/adjustments` | B | `{ description, amount }` (negative = credit) |

### Billing, overdue, reminders (Trường)

A background job runs hourly (idempotent per day): issues each month's rent invoice 7 days before it is due
(`INV<yyyyMM>C<contractId>`), marks unpaid invoices `Overdue`, opens an overdue case after `graceDays`, adds a
late-fee line of `lateFeePercentPerWeek` per full week past grace, locks out at `suspendAfterDays` (access
codes suspended, contract `Suspended`), flags `ClearOut` at `clearOutAfterDays`, and emails reminders 3 days
before due and weekly once overdue (honouring `emailReminders`). Paying in full resolves the case and
restores access.

| Method & route | Who |
|---|---|
| `POST /api/billing/run?date=2026-10-14` | B, A — run the job now |
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
| `GET /api/activity-logs?userId=&action=&from=&to=` | A — every write by a signed-in user, plus logins |

Reset the local demo database: `powershell -ExecutionPolicy Bypass -File scripts/reset-demo.ps1`.
