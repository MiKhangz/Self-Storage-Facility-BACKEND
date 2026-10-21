# Trường tasks - implementation and verification

This document records the implementation for T-01 through T-04 from `TRUONG_TASKS.md`.

## T-01: renewal approval

Endpoints:

- `POST /api/renewals/{id}/approve`
- `POST /api/renewals/{id}/reject`

`FacilityManager` is restricted by `StaffScope`; `BusinessOperationsManager` can process every facility. An
out-of-scope renewal returns `404`. A renewal must be `Requested`; otherwise the API returns `409`. Approval also
requires an `Active` contract and updates `EndDate` and `MonthlyRent`. Rejection updates only the renewal status and
approver.

## T-02: VNPay sandbox

The default provider remains `Mock`, so local demos continue to work offline. To use VNPay, configure these values
through Development user-secrets or an ignored `appsettings.Development.json`:

```json
{
  "PaymentGateway": {
    "Provider": "VNPay",
    "VnPayTmnCode": "YOUR_TMN_CODE",
    "VnPayHashSecret": "YOUR_HASH_SECRET",
    "VnPayPaymentUrl": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
    "VnPayReturnUrl": "https://YOUR_PUBLIC_HOST/api/payments/vnpay-return",
    "VnPayIpAddress": "127.0.0.1"
  }
}
```

When configured, `POST /api/invoices/{id}/payments/online` returns a signed VNPay checkout URL. The following
endpoints verify the VNPay signature and settle the payment idempotently:

- `GET /api/payments/vnpay-return`
- `GET /api/payments/vnpay-ipn`
- `POST /api/payments/vnpay-ipn` (form callback)

The existing `POST /api/payments/gateway-callback` remains available for the internal signed callback, and
`POST /api/payments/{paymentCode}/simulate-success` remains Development-only.

## T-03: draft pricing policy update

`PUT /api/pricing-policies/{id}` is restricted to `BusinessOperationsManager`. It accepts the same fields and
validation rules as create, including `EarlyTerminationPercent` in the range `0..100`, where `2.00` means 2 percent.
Only `Draft` policies can be edited; other statuses return `409`.

## T-04: facility scope

Contracts, access codes, access logs, handovers, inspections, overdue cases, support tickets and maintenance now use
the same visibility rule: facility staff/managers see only their assigned facilities, while business operations and
system administrators see all facilities. An inaccessible resource is returned as `404`.

## Verification

From `BE/`:

```powershell
dotnet build StorEase.sln --no-restore
dotnet test StorEase.sln --no-restore
```

After route or DTO changes, regenerate the committed OpenAPI document from the repository root:

```powershell
.\scripts\export-openapi.ps1
```

No database schema change or migration is required for these tasks.
