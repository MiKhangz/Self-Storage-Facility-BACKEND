# Trường Core API Guide

Tài liệu này hướng dẫn sử dụng 9 task Core backend của Trường.

## 1. Chạy API và mở Swagger

Từ thư mục `BE`:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=localhost\SQLEXPRESS;Database=StorEase;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
$env:Jwt__Key = "StorEase-local-development-key-2026-strong-32-chars"
dotnet run --project StorEase.Api --launch-profile http
```

Mở Swagger:

```text
http://localhost:5108/swagger/index.html
```

API yêu cầu database `StorEase` đã có schema và dữ liệu cần thiết. Tài liệu này không tạo migration hoặc thay đổi schema.

## 2. Các file đã thêm/sửa

### Application

- `BE/StorEase.Application/DependencyInjection.cs`
- `BE/StorEase.Application/Features/StorageUnits/UnitCatalogService.cs`
- `BE/StorEase.Application/Features/StorageUnits/Dtos/UnitCatalogDtos.cs`
- `BE/StorEase.Application/Features/StorageUnits/Validators/UnitTypeValidators.cs`
- `BE/StorEase.Application/Features/Pricing/PricingService.cs`
- `BE/StorEase.Application/Features/Pricing/Dtos/PricingDtos.cs`
- `BE/StorEase.Application/Features/Pricing/Validators/PricingValidators.cs`
- `BE/StorEase.Application/Features/Invoices/InvoiceService.cs`
- `BE/StorEase.Application/Features/Invoices/Dtos/InvoiceDtos.cs`
- `BE/StorEase.Application/Features/Invoices/Validators/PaymentValidators.cs`

### API Controllers

- `BE/StorEase.Api/Controllers/UnitTypesController.cs`
- `BE/StorEase.Api/Controllers/PricingPoliciesController.cs`
- `BE/StorEase.Api/Controllers/InvoicesController.cs`

### Tests

- `BE/tests/StorEase.Application.Tests/TruongCoreFeatureTests.cs`

Không thay đổi các entity, EF configuration hoặc migration hiện có.

## 3. 9 task Core đã triển khai

| Task | Chức năng | Endpoint chính |
|---|---|---|
| BE-08 | Unit type list & size guide | `GET /api/unit-types` |
| BE-09 | Availability check | `GET /api/unit-types/{id}/availability` |
| BE-10 | Price quote | `GET /api/unit-types/{id}/quote` |
| BE-11 | Unit type CRUD | `POST/GET/PUT/DELETE /api/unit-types` |
| BE-12 | Pricing policy versions | `GET/POST /api/pricing-policies`, `POST /{id}/activate` |
| BE-13 | Unit price table | `PUT /api/pricing-policies/{id}/unit-prices/{unitTypeId}` |
| BE-39 | Invoice list | `GET /api/invoices` |
| BE-41 | Online payment | `POST /api/invoices/{id}/payments/online` |
| BE-44 | Manual payment | `POST /api/invoices/{id}/payments/manual` |

## 4. Quyền truy cập

- Public:
  - `GET /api/unit-types`
  - `GET /api/unit-types/{id}`
  - `GET /api/unit-types/{id}/availability`
  - `GET /api/unit-types/{id}/quote`
- `BusinessOperationsManager`:
  - Unit Type CRUD
  - Pricing policy
  - Unit price table
- `StorageCustomer`:
  - Xem invoice của chính mình
  - Online payment invoice của chính mình
- `FacilityStaff`, `FacilityManager`:
  - Xem invoice trong facility scope
  - Manual payment

Sau khi gọi `/api/auth/login`, bấm **Authorize** trên Swagger và nhập:

```text
Bearer <JWT_TOKEN>
```

## 5. Thứ tự test đề xuất

### Bước 1: Unit Catalog

```http
GET /api/unit-types
```

Lấy `unitTypeId`, sau đó gọi:

```http
GET /api/unit-types/{unitTypeId}/availability?facilityId=1&moveInDate=2026-10-20
```

```http
GET /api/unit-types/{unitTypeId}/quote?facilityId=1&moveInDate=2026-10-20&rentalMonths=3
```

Price quote cần có:

- Facility đang `Active`.
- Storage unit đang `Available`.
- Pricing policy đang `Active` và áp dụng cho ngày move-in.
- Unit price tương ứng với unit type.

### Bước 2: Tạo pricing policy mới

```http
POST /api/pricing-policies
Content-Type: application/json
```

```json
{
  "effectiveFrom": "2027-01-01",
  "effectiveTo": null,
  "depositMonths": 2,
  "graceDays": 3,
  "noticeDays": 30,
  "lateFeePercentPerWeek": 2,
  "suspendAfterDays": 14,
  "clearOutAfterDays": 60,
  "cancelFreeHours": 24
}
```

Lấy `pricingPolicyId`, sau đó thêm giá:

```http
PUT /api/pricing-policies/{pricingPolicyId}/unit-prices/{unitTypeId}
Content-Type: application/json
```

```json
{
  "basePricePerMonth": 2000000,
  "discount3Month": 5,
  "discount6Month": 10,
  "discount12Month": 15
}
```

Cuối cùng activate policy:

```http
POST /api/pricing-policies/{pricingPolicyId}/activate
```

Policy phải có ít nhất một unit price trước khi activate. Policy cũ sẽ chuyển sang `Superseded`.

### Bước 3: Invoice và payment

Xem invoice:

```http
GET /api/invoices?page=1&pageSize=20
```

Online payment:

```http
POST /api/invoices/{invoiceId}/payments/online
Content-Type: application/json
```

```json
{
  "amount": 500000,
  "referenceNo": "DEMO-GATEWAY-001"
}
```

Manual payment:

```http
POST /api/invoices/{invoiceId}/payments/manual
Content-Type: application/json
```

```json
{
  "amount": 500000,
  "referenceNo": "CASH-001"
}
```

Payment sẽ bị từ chối nếu:

- Invoice không tồn tại hoặc không thuộc quyền xem.
- Invoice đã `Paid` hoặc `WrittenOff`.
- Số tiền thanh toán lớn hơn outstanding amount.

## 6. Kiểm tra code

Từ thư mục `BE`:

```powershell
dotnet build StorEase.sln --no-restore
dotnet test StorEase.sln --no-restore
```

Kết quả hiện tại: `126 passed, 0 failed`.

Online payment hiện là backend capture nội bộ với `PaymentStatus.Succeeded`; repo chưa có payment gateway sandbox bên ngoài.
