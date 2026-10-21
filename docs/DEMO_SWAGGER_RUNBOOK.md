# Runbook kiểm thử Swagger - branch demo

Phạm vi: kiểm thử 9 task Core của Trường bằng Swagger, không chạy migration và không thay đổi schema.

## 1. Khởi động API

Mở PowerShell tại thư mục `BE`:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=localhost\SQLEXPRESS;Database=StorEase;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
$env:Jwt__Key = "StorEase-local-development-key-2026-strong-32-chars"
dotnet run --project StorEase.Api --launch-profile http
```

Nếu database chưa có hoặc chưa có dữ liệu demo, chạy setup/seed từ thư mục repo:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=localhost\SQLEXPRESS;Database=StorEase;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
dotnet run --project scripts\StorEase.DbSetup\StorEase.DbSetup.csproj
```

Utility này chạy các migration hiện có rồi seed dữ liệu idempotent. Chạy lại không tạo bản ghi demo trùng.

Nếu không muốn chạy .NET utility, mở file [StorEase_Database_Demo.sql](./StorEase_Database_Demo.sql) bằng SQL Server Management Studio hoặc Azure Data Studio và chạy toàn bộ file. File SQL này đã gồm cả schema hiện tại và demo seed data.

Mở Swagger:

```text
http://localhost:5108/swagger/index.html
```

Trước khi test, database phải có facility `Active`, unit type, storage unit `Available`, active pricing policy, unit price và invoice `Issued`.

Lưu ý: source hiện không có `DbSeeder` chạy tự động. Nếu tài khoản demo không tồn tại, cần dùng tài khoản đã có trong database hoặc seed dữ liệu trước.

## 2. Đăng nhập và Authorize

Gọi `POST /api/auth/login`:

```json
{
  "email": "customer@storease.vn",
  "password": "Demo@123"
}
```

Copy trường `token`, bấm **Authorize** ở góc phải Swagger và nhập:

```text
Bearer <token>
```

Các role cần dùng:

- `StorageCustomer`: xem quote, invoice và online payment.
- `BusinessOperationsManager`: Unit Type CRUD, pricing policy, unit price.
- `FacilityStaff` hoặc `FacilityManager`: xem invoice theo facility và manual payment.

Khi đổi role, login lại và thay token trong **Authorize**.

## 3. Kịch bản chính

### A. Unit Catalog

1. `GET /api/unit-types` → lấy `unitTypeId`.
2. `GET /api/facilities?page=1&pageSize=20` → lấy `facilityId`.
3. `GET /api/unit-types/{unitTypeId}/availability?facilityId={facilityId}&moveInDate=2026-10-20`.
4. `GET /api/unit-types/{unitTypeId}/quote?facilityId={facilityId}&moveInDate=2026-10-20&rentalMonths=3`.

Kỳ vọng quote có `monthlyRent`, `rentSubtotal`, `depositAmount`, `fees`, `totalAmount`. Quote dùng policy `Active` có hiệu lực tại ngày move-in và áp dụng discount theo số tháng.

### B. Unit Type CRUD

Đổi token sang `BusinessOperationsManager` rồi gọi `POST /api/unit-types`:

```json
{
  "code": "DEMO",
  "name": "Demo Unit",
  "areaM2": 6,
  "width": 2,
  "depth": 3,
  "height": 2,
  "isClimateControlled": false
}
```

Sau đó gọi:

- `GET /api/unit-types/{id}`.
- `PUT /api/unit-types/{id}` với dữ liệu cập nhật.
- `DELETE /api/unit-types/{id}`.

Unit type chưa được tham chiếu sẽ trả `204`. Unit type đã có `StorageUnit` hoặc `UnitPrice` phải trả `409`.

### C. Pricing Policy và Unit Price

Vẫn dùng role `BusinessOperationsManager`.

`POST /api/pricing-policies`:

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

Lấy `pricingPolicyId`, sau đó gọi `PUT /api/pricing-policies/{policyId}/unit-prices/{unitTypeId}`:

```json
{
  "basePricePerMonth": 2000000,
  "discount3Month": 5,
  "discount6Month": 10,
  "discount12Month": 15
}
```

Gọi `POST /api/pricing-policies/{policyId}/activate`. Kỳ vọng policy mới thành `Active`, policy cũ thành `Superseded`. Policy không có unit price phải trả `409`.

### D. Invoice và Payment

Đổi token sang `StorageCustomer`:

1. `GET /api/invoices?page=1&pageSize=20`.
2. `GET /api/invoices/{invoiceId}`.
3. `POST /api/invoices/{invoiceId}/payments/online`:

```json
{
  "amount": 500000,
  "referenceNo": "DEMO-GATEWAY-001"
}
```

Kỳ vọng payment có `method = Gateway`, `status = Succeeded`, `paidAmount` tăng và invoice thành `Paid` nếu thanh toán đủ.

Đổi token sang `FacilityStaff` hoặc `FacilityManager`, sau đó gọi `POST /api/invoices/{invoiceId}/payments/manual`:

```json
{
  "amount": 100000,
  "referenceNo": "CASH-DEMO-001"
}
```

Kỳ vọng payment có `method = Cash`, `status = Succeeded`. Staff chỉ thanh toán được invoice trong facility scope.

Online payment hiện là capture nội bộ vì repo chưa có gateway sandbox bên ngoài.

## 4. Các case lỗi bắt buộc

| Case | Kỳ vọng |
|---|---|
| Không có JWT gọi API protected | `401` |
| Customer tạo pricing policy | `403` |
| Staff gọi online payment | `403` |
| Amount bằng 0 hoặc âm | `400` |
| Discount lớn hơn 100 | `400` |
| Quote facility không tồn tại/inactive | `404` |
| Quote thiếu UnitPrice | `404` |
| Activate policy không có UnitPrice | `409` |
| Payment vượt outstanding amount | `409` |
| Payment invoice đã `Paid`/`WrittenOff` | `409` |
| Customer xem invoice người khác | `404` |

Sau mỗi case payment lỗi, kiểm tra lại invoice: `paidAmount` không được tăng và không có Payment mới được tạo.

## 5. Checklist kết thúc demo

- [ ] Swagger mở được tại `http://localhost:5108/swagger/index.html`.
- [ ] Login trả JWT và **Authorize** hoạt động.
- [ ] Unit type list/detail hoạt động.
- [ ] Availability và quote trả số liệu hợp lý.
- [ ] Pricing policy tạo, thêm unit price và activate thành công.
- [ ] Invoice list chỉ trả dữ liệu đúng quyền.
- [ ] Online/manual payment cập nhật invoice đúng.
- [ ] Các case `400`, `403`, `404`, `409` trả đúng.
- [ ] Chạy lại `dotnet test StorEase.sln --no-restore` sau demo.

Kịch bản đầy đủ hơn nằm trong [DEMO_API_TEST_SCENARIO.md](./DEMO_API_TEST_SCENARIO.md).
