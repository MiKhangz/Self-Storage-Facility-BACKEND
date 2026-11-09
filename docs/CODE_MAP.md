# Code map: who owns which file

Generated from the real folders by `python scripts/code_map.py --write` (the same rules draw the "Code map" tab in `docs/flows.drawio`).
Each file has one owner, who answers for it, writes its tests and reviews changes to it. **Shared** files: anyone
edits, Khang reviews. Flow owners are in `docs/TEAM_FLOWS.md`.

Paths start with `BE/` (backend repo) or the frontend repo root. Old unrouted screens in `src/admin/` are not listed.

| Person | Flows | Backend files | Frontend files | E2E tests |
|---|---|---|---|---|
| **Khang** | F01, F02, F03, F04, F14, F15 | 40 | 32 | E01, E02, E03, E04, E09, E10, E13, E14, E17, E18, E20, E21 |
| **Trường** | F05, F07, F08, F09, F13 | 52 | 12 | E06, E07, E16, E23 |
| **Thiện** | F06, F10, F11, F12 | 75 | 26 | E05, E05b, E05c, E08, E11, E12, E15, E19, E22 |

## Khang

### Account & sign-in (F01)

- **Controllers and routes**
  - `BE/StorEase.Api/Controllers/AuthController.cs`: `POST /api/auth/login`, `GET /api/auth/session`, `POST /api/auth/logout`, `POST /api/auth/register`, `POST /api/auth/verify-email`, `POST /api/auth/resend-verification`, `POST /api/auth/forgot-password`, `POST /api/auth/reset-password`
- **Services, DTOs, validators, jobs**
  - `BE/StorEase.Application/Features/Auth/AuthService.cs`
  - `BE/StorEase.Application/Features/Auth/Dtos/AccountDtos.cs`
  - `BE/StorEase.Application/Features/Auth/Dtos/LoginRequest.cs`
  - `BE/StorEase.Application/Features/Auth/Dtos/LoginResponse.cs`
  - `BE/StorEase.Application/Features/Auth/Dtos/SessionResponse.cs`
  - `BE/StorEase.Application/Features/Auth/Validators/AccountValidators.cs`
  - `BE/StorEase.Application/Features/Auth/Validators/LoginRequestValidator.cs`
- **Backend tests**
  - `BE/tests/StorEase.Application.Tests/AccountAndPricingTests.cs`
  - `BE/tests/StorEase.Application.Tests/AuthServiceTests.cs`
  - `BE/tests/StorEase.Application.Tests/SmtpEmailServiceTests.cs`
- **Frontend**
  - `src/customer/auth/AuthShell.jsx`
  - `src/customer/auth/ForgotPasswordPage.jsx`
  - `src/customer/auth/LoginPage.jsx`
  - `src/customer/auth/RegisterPage.jsx`
  - `src/customer/auth/ResendVerification.jsx`
  - `src/customer/auth/VerifyEmailPage.jsx`
  - `src/features/auth/AuthContext.tsx`
  - `src/features/auth/RequireAuth.tsx`
  - `src/features/auth/roles.ts`
  - `src/staff/AdminLoginPage.jsx`
- **Frontend tests**
  - `e2e/01-account.spec.js`: E01, E20, E21

### Profile & settings (F02)

- **Controllers and routes**
  - `BE/StorEase.Api/Controllers/ProfileController.cs`: `GET /api/users/me`, `PUT /api/users/me`, `POST /api/users/me/password`, `GET /api/users/me/notification-settings`, `PUT /api/users/me/notification-settings`
- **Services, DTOs, validators, jobs**
  - `BE/StorEase.Application/Features/Users/ProfileService.cs`
- **Frontend**
  - `src/customer/account/ProfilePage.jsx`

### Reservations, deposit & walk-in (F03 F04)

- **Controllers and routes**
  - `BE/StorEase.Api/Controllers/ReservationsController.cs`: `POST /api/reservations/walk-in`, `POST /api/reservations`, `GET /api/reservations`, `GET /api/reservations/{id}`, `POST /api/reservations/{id}/cancel`, `POST /api/reservations/{id}/no-show`, `GET /api/reservations/{id}/handover`, `POST /api/reservations/{id}/assign-unit`, `POST /api/reservations/{id}/change-unit`
- **Services, DTOs, validators, jobs**
  - `BE/StorEase.Application/Features/Reservations/Dtos/AssignUnitRequest.cs`
  - `BE/StorEase.Application/Features/Reservations/Dtos/CreateReservationRequest.cs`
  - `BE/StorEase.Application/Features/Reservations/Dtos/ReservationDto.cs`
  - `BE/StorEase.Application/Features/Reservations/ReservationService.cs`
  - `BE/StorEase.Application/Features/Reservations/Validators/AssignUnitRequestValidator.cs`
  - `BE/StorEase.Application/Features/Reservations/Validators/CancelReservationRequestValidator.cs`
  - `BE/StorEase.Application/Features/Reservations/Validators/CreateReservationRequestValidator.cs`
  - `BE/StorEase.Application/Features/Reservations/WalkInService.cs`
- **Backend tests**
  - `BE/tests/StorEase.Application.Tests/ReservationServiceTests.cs`
  - `BE/tests/StorEase.Application.Tests/WalkInTests.cs`
- **Frontend**
  - `src/customer/booking/BookingConfirmedPage.jsx`
  - `src/customer/booking/BookingDetailsPage.jsx`
  - `src/customer/booking/BookingStepper.jsx`
  - `src/customer/booking/BookingSummary.jsx`
  - `src/customer/booking/CancelBooking.jsx`
  - `src/customer/booking/ChooseUnitPage.jsx`
  - `src/staff/pages/ReservationsPage.jsx`
  - `src/staff/pages/WalkInBookingPage.jsx`
- **Frontend tests**
  - `e2e/03-desk.spec.js`: E09, E10

### Support tickets (F15)

- **Controllers and routes**
  - `BE/StorEase.Api/Controllers/SupportTicketsController.cs`: `POST /api/support-tickets`, `GET /api/support-tickets/{id}`, `GET /api/support-tickets/{id}/messages`, `POST /api/support-tickets/{id}/messages`
  - `BE/StorEase.Api/Controllers/TicketCategoriesController.cs`: `GET /api/ticket-categories`
  - `BE/StorEase.Api/Controllers/TicketQueueController.cs`: `GET /api/support-tickets`, `POST /api/support-tickets/{id}/assign`, `POST /api/support-tickets/{id}/status`
- **Services, DTOs, validators, jobs**
  - `BE/StorEase.Application/Features/SupportTickets/Dtos/CreateTicketRequest.cs`
  - `BE/StorEase.Application/Features/SupportTickets/Dtos/PostTicketMessageRequest.cs`
  - `BE/StorEase.Application/Features/SupportTickets/Dtos/TicketDto.cs`
  - `BE/StorEase.Application/Features/SupportTickets/Dtos/TicketQueueItemDto.cs`
  - `BE/StorEase.Application/Features/SupportTickets/Dtos/TicketQueueRequests.cs`
  - `BE/StorEase.Application/Features/SupportTickets/SupportTicketService.cs`
  - `BE/StorEase.Application/Features/SupportTickets/TicketQueueService.cs`
  - `BE/StorEase.Application/Features/SupportTickets/TicketStatusRules.cs`
  - `BE/StorEase.Application/Features/SupportTickets/Validators/CreateTicketRequestValidator.cs`
  - `BE/StorEase.Application/Features/SupportTickets/Validators/PostTicketMessageRequestValidator.cs`
  - `BE/StorEase.Application/Features/SupportTickets/Validators/TicketQueueValidators.cs`
- **Backend tests**
  - `BE/tests/StorEase.Application.Tests/SupportTicketServiceTests.cs`
  - `BE/tests/StorEase.Application.Tests/TicketQueueServiceTests.cs`
- **Frontend**
  - `src/customer/support/SupportRequestPage.jsx`
  - `src/customer/support/TicketDetailPage.jsx`
  - `src/customer/support/support.js`
  - `src/staff/pages/TicketDetailPage.jsx`
  - `src/staff/pages/TicketsPage.jsx`

### Language (F14)

- **Frontend**
  - `src/i18n/I18n.jsx`
  - `src/i18n/en.js`
  - `src/i18n/fill.js`
  - `src/i18n/vi.js`
- **Frontend tests**
  - `e2e/06-language.spec.js`: E17, E18
  - `tests/fill.test.mjs`


## Trường

### Billing job & overdue (F08)

- **Controllers and routes**
  - `BE/StorEase.Api/Controllers/BillingController.cs`: `POST /api/billing/run`
  - `BE/StorEase.Api/Controllers/OverdueCasesController.cs`: `GET /api/overdue-cases`, `GET /api/overdue-cases/{id}`, `POST /api/overdue-cases/{id}/lockout`, `POST /api/overdue-cases/{id}/restore-access`
- **Services, DTOs, validators, jobs**
  - `BE/StorEase.Api/BillingWorker.cs`
  - `BE/StorEase.Application/Features/Billing/BillingCycleService.cs`
  - `BE/StorEase.Application/Features/Billing/ReminderService.cs`
  - `BE/StorEase.Application/Features/OverdueCases/OverdueRules.cs`
  - `BE/StorEase.Application/Features/OverdueCases/OverdueService.cs`
- **Backend tests**
  - `BE/tests/StorEase.Application.Tests/BillingCycleTests.cs`
  - `BE/tests/StorEase.Application.Tests/ScopeTermsAndBillingTests.cs`
- **Frontend**
  - `src/staff/pages/OverdueCasesPage.jsx`

### Contracts & customers (F05)

- **Controllers and routes**
  - `BE/StorEase.Api/Controllers/ContractsController.cs`: `POST /api/contracts`, `GET /api/contracts`, `GET /api/contracts/{id}`, `POST /api/contracts/{id}/sign`, `POST /api/contracts/{id}/sign-at-desk`, `GET /api/contracts/export`, `POST /api/contracts/{id}/terminate`, `POST /api/contracts/{id}/access-codes`, `GET /api/contracts/{id}/access-code`, `POST /api/contracts/{id}/access-codes/request`
  - `BE/StorEase.Api/Controllers/CustomersController.cs`: `GET /api/customers`, `GET /api/customers/{id}`
- **Services, DTOs, validators, jobs**
  - `BE/StorEase.Application/Features/Contracts/ContractQueries.cs`
  - `BE/StorEase.Application/Features/Contracts/ContractService.cs`
  - `BE/StorEase.Application/Features/Contracts/Dtos/ContractDto.cs`
  - `BE/StorEase.Application/Features/Contracts/IContractService.cs`
  - `BE/StorEase.Application/Features/Contracts/Validators/CreateContractRequestValidator.cs`
- **Backend tests**
  - `BE/tests/StorEase.Application.Tests/ContractLifecycleTests.cs`
  - `BE/tests/StorEase.Application.Tests/ContractServiceTests.cs`
- **Frontend**
  - `src/staff/pages/ContractsPage.jsx`
  - `src/staff/pages/CustomerPage.jsx`
  - `src/staff/print.js`

### Invoices, payments & pricing (F07)

- **Controllers and routes**
  - `BE/StorEase.Api/Controllers/FacilityPricesController.cs`: `GET /api/facilities/{facilityId:int}/price-versions`, `POST /api/facilities/{facilityId:int}/price-versions`, `PUT /api/facilities/{facilityId:int}/price-versions/{versionId}/unit-prices/{unitTypeId}`, `DELETE /api/facilities/{facilityId:int}/price-versions/{versionId}/unit-prices/{unitTypeId}`, `POST /api/facilities/{facilityId:int}/price-versions/{versionId}/publish`, `DELETE /api/facilities/{facilityId:int}/price-versions/{versionId}`
  - `BE/StorEase.Api/Controllers/InvoicesController.cs`: `GET /api/invoices`, `GET /api/invoices/{id}`, `POST /api/invoices/{id}/payments/online`, `POST /api/invoices/{id}/payments/manual`, `POST /api/invoices/{id}/adjustments`, `GET /api/invoices/{id}/reminders`, `POST /api/invoices/{id}/reminders`
  - `BE/StorEase.Api/Controllers/PaymentsController.cs`: `GET /api/payments`, `POST /api/payments/{id}/refund`, `POST /api/payments/gateway-callback`, `GET /api/payments/vnpay-ipn`, `GET /api/payments/vnpay-return`, `POST /api/payments/{paymentCode}/simulate-success`
  - `BE/StorEase.Api/Controllers/PricingPoliciesController.cs`: `GET /api/pricing-policies`, `GET /api/pricing-policies/current-terms`, `GET /api/pricing-policies/{id}`, `POST /api/pricing-policies`, `PUT /api/pricing-policies/{id}`, `POST /api/pricing-policies/{id}/copy`, `DELETE /api/pricing-policies/{id}`, `POST /api/pricing-policies/{id}/activate`, `PUT /api/pricing-policies/{id}/unit-prices/{unitTypeId}`, `POST /api/pricing-policies/{id}/fee-types`, `PUT /api/fee-types/{feeTypeId}`, `DELETE /api/fee-types/{feeTypeId}`
- **Services, DTOs, validators, jobs**
  - `BE/StorEase.Application/Features/Invoices/Dtos/InvoiceDtos.cs`
  - `BE/StorEase.Application/Features/Invoices/InvoiceBook.cs`
  - `BE/StorEase.Application/Features/Invoices/InvoiceService.cs`
  - `BE/StorEase.Application/Features/Invoices/Validators/InvoiceAdjustmentValidators.cs`
  - `BE/StorEase.Application/Features/Invoices/Validators/PaymentValidators.cs`
  - `BE/StorEase.Application/Features/Invoices/VnPay.cs`
  - `BE/StorEase.Application/Features/Pricing/Dtos/FacilityPriceDtos.cs`
  - `BE/StorEase.Application/Features/Pricing/Dtos/PricingDtos.cs`
  - `BE/StorEase.Application/Features/Pricing/FacilityPricingService.cs`
  - `BE/StorEase.Application/Features/Pricing/PricingService.cs`
  - `BE/StorEase.Application/Features/Pricing/RentCalculator.cs`
  - `BE/StorEase.Application/Features/Pricing/Validators/PricingValidators.cs`
- **Backend tests**
  - `BE/tests/StorEase.Application.Tests/FacilityPriceTests.cs`
  - `BE/tests/StorEase.Application.Tests/PricingDraftTests.cs`
  - `BE/tests/StorEase.Application.Tests/PricingPinTests.cs`
  - `BE/tests/StorEase.Application.Tests/StorageUnitPricingTests.cs`
  - `BE/tests/StorEase.Application.Tests/TruongAssignedTaskTests.cs`
  - `BE/tests/StorEase.Application.Tests/TruongCoreFeatureTests.cs`
  - `BE/tests/StorEase.Application.Tests/VnPayConfirmTests.cs`
  - `BE/tests/StorEase.Application.Tests/VnPayTests.cs`
- **Frontend**
  - `src/customer/account/PayInvoiceButton.jsx`
  - `src/customer/account/PaymentsPage.jsx`
  - `src/customer/account/VnPayReturnPage.jsx`
  - `src/staff/pages/FacilityPriceList.jsx`
  - `src/staff/pages/PricingPage.jsx`
  - `src/staff/pages/RecordPaymentPage.jsx`

### Renewals (F09)

- **Controllers and routes**
  - `BE/StorEase.Api/Controllers/RenewalsController.cs`: `POST /api/renewals`, `GET /api/renewals`, `GET /api/renewals/{id}`, `POST /api/renewals/{id}/approve`, `POST /api/renewals/{id}/reject`
- **Services, DTOs, validators, jobs**
  - `BE/StorEase.Application/Features/Renewals/Dtos/RenewalDtos.cs`
  - `BE/StorEase.Application/Features/Renewals/RenewalPricing.cs`
  - `BE/StorEase.Application/Features/Renewals/RenewalService.cs`
  - `BE/StorEase.Application/Features/Renewals/RenewalStatus.cs`
  - `BE/StorEase.Application/Features/Renewals/Validators/RenewalValidators.cs`
- **Backend tests**
  - `BE/tests/StorEase.Application.Tests/RenewalDecisionTests.cs`
  - `BE/tests/StorEase.Application.Tests/RenewalPricingTests.cs`

### Reports & dashboard (F13)

- **Controllers and routes**
  - `BE/StorEase.Api/Controllers/ReportsController.cs`: `GET /api/reports/dashboard`, `GET /api/reports/occupancy`, `GET /api/reports/revenue`
- **Services, DTOs, validators, jobs**
  - `BE/StorEase.Application/Features/Reports/ReportService.cs`
- **Frontend**
  - `src/staff/pages/DashboardPage.jsx`
  - `src/staff/pages/ReportsPage.jsx`


## Thiện

### Handover, visits & access codes (F06)

- **Controllers and routes**
  - `BE/StorEase.Api/Controllers/AccessCodesController.cs`: `POST /api/access-codes/{id}/revoke`
  - `BE/StorEase.Api/Controllers/CheckInsController.cs`: `POST /api/check-ins/check-in`, `POST /api/check-ins/{id}/check-out`, `POST /api/check-ins/{id}/mark-left`, `GET /api/check-ins`, `GET /api/check-ins/board`, `POST /api/check-ins/find`, `GET /api/check-ins/{id}`
  - `BE/StorEase.Api/Controllers/HandoversController.cs`: `POST /api/handovers`, `GET /api/handovers`, `POST /api/handovers/{id}/cancel`, `GET /api/handovers/{id}`, `POST /api/handovers/{id}/photos`, `GET /api/handovers/{id}/photos/{photoId}`
- **Services, DTOs, validators, jobs**
  - `BE/StorEase.Application/Features/AccessCodes/AccessCodeService.cs`
  - `BE/StorEase.Application/Features/AccessCodes/Dtos/AccessCodeDto.cs`
  - `BE/StorEase.Application/Features/AccessCodes/IAccessCodeService.cs`
  - `BE/StorEase.Application/Features/CheckIns/CheckInService.cs`
  - `BE/StorEase.Application/Features/Handovers/Dtos/CreateHandoverRequest.cs`
  - `BE/StorEase.Application/Features/Handovers/Dtos/HandoverDto.cs`
  - `BE/StorEase.Application/Features/Handovers/HandoverEvidenceService.cs`
  - `BE/StorEase.Application/Features/Handovers/HandoverService.cs`
  - `BE/StorEase.Application/Features/Handovers/Validators/CreateHandoverRequestValidator.cs`
- **Backend tests**
  - `BE/tests/StorEase.Application.Tests/AccessCodeManagementTests.cs`
  - `BE/tests/StorEase.Application.Tests/CheckInTests.cs`
  - `BE/tests/StorEase.Application.Tests/HandoverServiceTests.cs`
  - `BE/tests/StorEase.Application.Tests/HandoverViewingTests.cs`
  - `BE/tests/StorEase.Application.Tests/TestCodes.cs`
- **Frontend**
  - `src/customer/account/MyStoragePage.jsx`
  - `src/customer/account/UnitDetailPage.jsx`: its renewal part is co-owned by Trường (F09)
  - `src/staff/components/HandoverPhotos.jsx`
  - `src/staff/pages/CheckInPage.jsx`
  - `src/staff/pages/HandoverPage.jsx`
  - `src/staff/photoRules.js`
- **Frontend tests**
  - `tests/photoRules.test.mjs`

### Access logs (no flow tab)

- **Controllers and routes**
  - `BE/StorEase.Api/Controllers/AccessLogsController.cs`: `GET /api/access-logs`, `GET /api/access-logs/mine`
- **Services, DTOs, validators, jobs**
  - `BE/StorEase.Application/Features/AccessCodes/AccessLogService.cs`
  - `BE/StorEase.Application/Features/AccessCodes/IAccessLogService.cs`

### Staff accounts & audit (F12)

- **Controllers and routes**
  - `BE/StorEase.Api/Controllers/ActivityLogsController.cs`: `GET /api/activity-logs`
  - `BE/StorEase.Api/Controllers/StaffController.cs`: `GET /api/staff`, `GET /api/staff/{id}`, `POST /api/staff`, `PUT /api/staff/{id}`, `PUT /api/staff/{id}/facilities`
- **Services, DTOs, validators, jobs**
  - `BE/StorEase.Api/Middleware/ActivityLogFilter.cs`
  - `BE/StorEase.Application/Features/ActivityLogs/ActivityLogService.cs`
  - `BE/StorEase.Application/Features/Staff/Dtos/StaffAccountDtos.cs`
  - `BE/StorEase.Application/Features/Staff/StaffAccountService.cs`
  - `BE/StorEase.Application/Features/Staff/Validators/StaffAccountValidators.cs`
- **Backend tests**
  - `BE/tests/StorEase.Api.Tests/ActivityLogFilterTests.cs`
  - `BE/tests/StorEase.Application.Tests/ActivityLogServiceTests.cs`
  - `BE/tests/StorEase.Application.Tests/StaffAccountServiceTests.cs`
  - `BE/tests/StorEase.Application.Tests/StaffScopeTests.cs`
- **Frontend**
  - `src/staff/activityLabels.js`
  - `src/staff/components/FacilityPicker.jsx`
  - `src/staff/pages/InviteUserPage.jsx`
  - `src/staff/pages/PendingBackendPages.jsx`
  - `src/staff/pages/UsersPage.jsx`
  - `src/staff/permissions.js`
- **Frontend tests**
  - `tests/activityLabels.test.mjs`

### Hand-back & deposit settlement (F10)

- **Controllers and routes**
  - `BE/StorEase.Api/Controllers/DepositRefundsController.cs`: `GET /api/deposit-refunds`, `POST /api/deposit-refunds/{id}/paid`
  - `BE/StorEase.Api/Controllers/ReturnsController.cs`: `POST /api/returns`, `GET /api/returns`, `GET /api/returns/{id}`, `POST /api/returns/{id}/approve`, `POST /api/returns/{id}/inspection`, `POST /api/returns/{id}/settle-deposit`
- **Services, DTOs, validators, jobs**
  - `BE/StorEase.Application/Features/DepositRefunds/DepositRefundService.cs`
  - `BE/StorEase.Application/Features/Returns/DepositSettlement.cs`
  - `BE/StorEase.Application/Features/Returns/Dtos/ReturnDtos.cs`
  - `BE/StorEase.Application/Features/Returns/ReturnService.cs`
  - `BE/StorEase.Application/Features/Returns/Validators/ReturnValidators.cs`
- **Backend tests**
  - `BE/tests/StorEase.Application.Tests/DepositRefundTests.cs`
  - `BE/tests/StorEase.Application.Tests/DepositSettlementTests.cs`
  - `BE/tests/StorEase.Application.Tests/MoveOutDeadlineTests.cs`
  - `BE/tests/StorEase.Application.Tests/ReturnSettlementTests.cs`
- **Frontend**
  - `src/customer/account/ReturnPage.jsx`
  - `src/staff/pages/DepositRefundsPage.jsx`
  - `src/staff/pages/ReturnsPage.jsx`

### Facilities, units & maintenance (F11)

- **Controllers and routes**
  - `BE/StorEase.Api/Controllers/FacilitiesController.cs`: `GET /api/facilities`, `GET /api/facilities/{id}`, `POST /api/facilities`, `PUT /api/facilities/{id}`, `POST /api/facilities/{id}/deactivate`, `POST /api/facilities/{id}/activate`, `GET /api/facilities/{id}/unit-board`, `GET /api/facilities/{id}/floors`, `POST /api/facilities/{id}/floors`
  - `BE/StorEase.Api/Controllers/FloorsController.cs`: `PUT /api/floors/{id}`, `DELETE /api/floors/{id}`
  - `BE/StorEase.Api/Controllers/MaintenanceOrdersController.cs`: `POST /api/maintenance-orders`, `GET /api/maintenance-orders`, `GET /api/maintenance-orders/{id}`, `POST /api/maintenance-orders/{id}/status`
  - `BE/StorEase.Api/Controllers/StorageUnitsController.cs`: `GET /api/storage-units`, `GET /api/storage-units/{id}`, `POST /api/storage-units`, `PUT /api/storage-units/{id}`, `POST /api/storage-units/{id}/retire`, `POST /api/storage-units/{id}/restore`
  - `BE/StorEase.Api/Controllers/UnitTypesController.cs`: `GET /api/unit-types`, `GET /api/unit-types/{id}`, `GET /api/unit-types/{id}/availability`, `GET /api/unit-types/{id}/quote`, `POST /api/unit-types`, `PUT /api/unit-types/{id}`, `DELETE /api/unit-types/{id}`
- **Services, DTOs, validators, jobs**
  - `BE/StorEase.Application/Features/Facilities/Dtos/FacilityDtos.cs`
  - `BE/StorEase.Application/Features/Facilities/Dtos/FacilityListItemDto.cs`
  - `BE/StorEase.Application/Features/Facilities/FacilityService.cs`
  - `BE/StorEase.Application/Features/Facilities/FloorService.cs`
  - `BE/StorEase.Application/Features/Facilities/Validators/FacilityValidators.cs`
  - `BE/StorEase.Application/Features/Maintenance/Dtos/MaintenanceOrderDtos.cs`
  - `BE/StorEase.Application/Features/Maintenance/MaintenanceService.cs`
  - `BE/StorEase.Application/Features/Maintenance/Validators/MaintenanceValidators.cs`
  - `BE/StorEase.Application/Features/StorageUnits/Dtos/StorageUnitDtos.cs`
  - `BE/StorEase.Application/Features/StorageUnits/Dtos/StorageUnitListItemDto.cs`
  - `BE/StorEase.Application/Features/StorageUnits/Dtos/UnitCatalogDtos.cs`
  - `BE/StorEase.Application/Features/StorageUnits/StorageUnitService.cs`
  - `BE/StorEase.Application/Features/StorageUnits/UnitCatalogService.cs`
  - `BE/StorEase.Application/Features/StorageUnits/Validators/StorageUnitValidators.cs`
  - `BE/StorEase.Application/Features/StorageUnits/Validators/UnitTypeValidators.cs`
- **Backend tests**
  - `BE/tests/StorEase.Application.Tests/FloorStatusTests.cs`
  - `BE/tests/StorEase.Application.Tests/MaintenanceServiceTests.cs`
  - `BE/tests/StorEase.Application.Tests/UnstaffedFacilityTests.cs`
- **Frontend**
  - `src/customer/locations/LocationsPage.jsx`
  - `src/customer/sizes/SizesPage.jsx`
  - `src/staff/pages/FacilitiesPage.jsx`
  - `src/staff/pages/FacilityDetailPage.jsx`
  - `src/staff/pages/MaintenancePage.jsx`
  - `src/staff/pages/UnitSizesPage.jsx`
  - `src/staff/pages/UnitsPage.jsx`
- **Frontend tests**
  - `tests/management-csv.test.mjs`

### Shifts & staff tasks (F12)

- **Controllers and routes**
  - `BE/StorEase.Api/Controllers/ShiftsController.cs`: `POST /api/shifts`, `GET /api/shifts`, `GET /api/shifts/{id}`, `PUT /api/shifts/{id}`, `DELETE /api/shifts/{id}`
  - `BE/StorEase.Api/Controllers/StaffTasksController.cs`: `POST /api/staff-tasks`, `GET /api/staff-tasks`, `GET /api/staff-tasks/{id}`, `POST /api/staff-tasks/{id}/status`
- **Services, DTOs, validators, jobs**
  - `BE/StorEase.Application/Features/Staff/Dtos/ShiftDtos.cs`
  - `BE/StorEase.Application/Features/Staff/Dtos/StaffTaskDtos.cs`
  - `BE/StorEase.Application/Features/Staff/ShiftService.cs`
  - `BE/StorEase.Application/Features/Staff/StaffTaskService.cs`
  - `BE/StorEase.Application/Features/Staff/Validators/ShiftValidators.cs`
  - `BE/StorEase.Application/Features/Staff/Validators/StaffTaskValidators.cs`
- **Backend tests**
  - `BE/tests/StorEase.Application.Tests/ShiftServiceTests.cs`
  - `BE/tests/StorEase.Application.Tests/StaffTaskServiceTests.cs`
- **Frontend**
  - `src/staff/pages/ShiftsPage.jsx`


## Shared

### Shared (no flow tab)

- **Controllers and routes**
  - `BE/StorEase.Api/Controllers/ApiControllerBase.cs`
- **Services, DTOs, validators, jobs**
  - `BE/StorEase.Api/CurrentUser.cs`
  - `BE/StorEase.Api/Middleware/ExceptionHandlingMiddleware.cs`
  - `BE/StorEase.Api/Middleware/ValidationFilter.cs`
  - `BE/StorEase.Api/Program.cs`
- **Backend tests**
  - `BE/tests/StorEase.Application.Tests/CoreFlowSeed.cs`
  - `BE/tests/StorEase.Application.Tests/ErrorTextTests.cs`
  - `BE/tests/StorEase.Application.Tests/ExistingDataCleanupTests.cs`
  - `BE/tests/StorEase.Application.Tests/FakeCurrentUser.cs`
  - `BE/tests/StorEase.Application.Tests/FakeEmailService.cs`
  - `BE/tests/StorEase.Application.Tests/TestDb.cs`
- **Frontend**
  - `src/api/axiosClient.js`
  - `src/customer/account/AccountLayout.jsx`
  - `src/customer/account/queries.js`
  - `src/customer/components/Icon.jsx`
  - `src/customer/components/PageHead.jsx`
  - `src/customer/components/confirm.js`
  - `src/customer/components/format.js`
  - `src/customer/components/terms.js`
  - `src/customer/home/HomePage.jsx`
  - `src/customer/layout/CustomerLayout.jsx`
  - `src/main.tsx`
  - `src/staff/AdminLayout.jsx`
  - `src/staff/components/ui.jsx`
  - `src/staff/queries.js`
- **Frontend tests**
  - `e2e/02-rental.spec.js`; also holds E02 (Khang), E03 (Khang), E04 (Khang), E05 (Thiện), E05b (Thiện), E05c (Thiện), E06 (Trường), E07 (Trường), E08 (Thiện)
  - `e2e/04-admin.spec.js`; also holds E11 (Thiện), E12 (Thiện), E16 (Trường), E19 (Thiện), E22 (Thiện), E23 (Trường)
  - `e2e/05-cancel.spec.js`; also holds E13 (Khang), E14 (Khang), E15 (Thiện)
  - `e2e/helpers.js`
  - `e2e/start-backend.mjs`

## Shared folders (not listed file by file)

- `StorEase.Domain (entities, enums)`
- `StorEase.Infrastructure (DbContext, migrations, email, file storage)`
- `StorEase.Application/Common`
- `src/api/generated (regenerate with npm run api:gen; never edit by hand)`

## Where do I start?

1. Open your flow tab in `docs/flows.drawio`; each step names the screen or job.
2. Find the controller above for the route the screen calls (the browser Network tab shows it), then its service in `Features/`.
3. Run your E2E tests: `npx playwright test -g "E06"`; backend tests: `dotnet test --filter <TestClass>`.
4. A new file? Add it to `RULES` in `scripts/code_map.py` and run `python scripts/code_map.py --write`, so the map stays complete.
