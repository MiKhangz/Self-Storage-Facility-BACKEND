# Kịch bản kiểm thử API cho nhánh Demo

Mục tiêu: chứng minh backend chạy được theo một luồng demo hoàn chỉnh, đồng thời kiểm tra riêng 9 task Core của Trường.

Thời lượng đề xuất: 15–20 phút.

## 1. Chuẩn bị trước khi demo

### Chạy backend

Từ thư mục `BE`:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=localhost\SQLEXPRESS;Database=StorEase;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
$env:Jwt__Key = "StorEase-local-development-key-2026-strong-32-chars"
dotnet run --project StorEase.Api --launch-profile http
```

Swagger:

```text
http://localhost:5108/swagger/index.html
```

### Dữ liệu cần có

Database phải có:

- Ít nhất một facility `Active`.
- Ít nhất một floor và storage unit `Available`.
- Ít nhất một `UnitType`.
- Một pricing policy `Active`.
- Một `UnitPrice` cho unit type cần test.
- Một invoice `Issued` để test payment.
- Ba tài khoản demo hoặc tài khoản tương đương:

| Role | Mục đích |
|---|---|
| `StorageCustomer` | Browse, quote, invoice, online payment |
| `FacilityStaff` | Xem invoice scope, manual payment |
| `BusinessOperationsManager` | Unit Type CRUD, pricing policy, unit price |

Không hard-code ID nếu database khác dữ liệu mẫu. Lấy ID từ response trước đó.

## 2. Quy ước kết quả

| HTTP | Ý nghĩa |
|---|---|
| `200` | Đọc hoặc cập nhật thành công |
| `201` | Tạo mới thành công |
| `204` | Xoá thành công |
| `400` | Request sai validation |
| `401` | Chưa đăng nhập hoặc token sai |
| `403` | Sai role |
| `404` | Không tìm thấy hoặc không thuộc quyền xem |
| `409` | Vi phạm business rule |

Mỗi lỗi business phải trả về `ProblemDetails` có `title` giải thích nguyên nhân.

## 3. Kịch bản chính trên Swagger

### Bước 1 — Đăng nhập customer

```http
POST /api/auth/login
```

```json
{
  "email": "customer@storease.vn",
  "password": "Demo@123"
}
```

Kỳ vọng: `200`, nhận `token` và role `StorageCustomer`.

Bấm **Authorize** trên Swagger, nhập:

```text
Bearer <token>
```

### Bước 2 — Xem Unit Type

```http
GET /api/unit-types
GET /api/unit-types/{unitTypeId}
```

Kỳ vọng:

- `200`.
- Có code, tên, diện tích, kích thước và climate controlled.

### Bước 3 — Kiểm tra availability

```http
GET /api/unit-types/{unitTypeId}/availability?facilityId={facilityId}&moveInDate=2026-10-20
```

Kỳ vọng:

- `200`.
- Có `availableUnits` và `totalUnits`.
- Unit `Available` được tính vào số lượng khả dụng.
- Unit `Reserved`, `Occupied`, `Maintenance`, `Retired` không được tính vào số lượng khả dụng.

### Bước 4 — Tính price quote

```http
GET /api/unit-types/{unitTypeId}/quote?facilityId={facilityId}&moveInDate=2026-10-20&rentalMonths=3
```

Kỳ vọng:

- `200`.
- Trả về monthly rent, rent subtotal, deposit, fees và total amount.
- Discount 3/6/12 tháng được áp dụng đúng tier.
- Quote sử dụng pricing policy có hiệu lực tại ngày move-in.

### Bước 5 — Đăng nhập Business Operations Manager

Đăng nhập lại bằng account có role `BusinessOperationsManager`, sau đó thay token trong Swagger.

### Bước 6 — Tạo pricing policy version

```http
POST /api/pricing-policies
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

Kỳ vọng: `201`, policy mới có status `Draft` và version tăng lên.

### Bước 7 — Thiết lập unit price

```http
PUT /api/pricing-policies/{pricingPolicyId}/unit-prices/{unitTypeId}
```

```json
{
  "basePricePerMonth": 2000000,
  "discount3Month": 5,
  "discount6Month": 10,
  "discount12Month": 15
}
```

Kỳ vọng: `200`, trả về UnitPrice tương ứng với policy và unit type.

### Bước 8 — Activate policy

```http
POST /api/pricing-policies/{pricingPolicyId}/activate
```

Kỳ vọng:

- `200`.
- Policy mới chuyển thành `Active`.
- Policy active trước đó chuyển thành `Superseded`.
- Policy không có unit price bị từ chối với `409`.

Quay lại Bước 4 với ngày move-in năm 2027 để chứng minh quote đã dùng policy mới.

### Bước 9 — Test Unit Type CRUD

Đang dùng token `BusinessOperationsManager`.

Tạo:

```http
POST /api/unit-types
```

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

Sau đó test:

```http
PUT /api/unit-types/{unitTypeId}
GET /api/unit-types/{unitTypeId}
DELETE /api/unit-types/{unitTypeId}
```

Kỳ vọng:

- Create: `201`.
- Update: `200`.
- Get: `200` với dữ liệu mới.
- Delete type chưa được tham chiếu: `204`.
- Delete type đã có StorageUnit hoặc UnitPrice: `409`, không xoá dữ liệu.

### Bước 10 — Đăng nhập customer và xem invoice

Đổi token về `StorageCustomer`.

```http
GET /api/invoices?page=1&pageSize=20
GET /api/invoices/{invoiceId}
```

Kỳ vọng:

- `200`.
- Customer chỉ nhìn thấy invoice của chính mình.
- Response có total amount, paid amount, outstanding amount, items và payments.

### Bước 11 — Online payment

```http
POST /api/invoices/{invoiceId}/payments/online
```

```json
{
  "amount": 500000,
  "referenceNo": "DEMO-GATEWAY-001"
}
```

Kỳ vọng:

- `200`.
- Payment có method `Gateway`, status `Succeeded`.
- `PaidAmount` tăng đúng số tiền.
- Nếu đủ tiền, invoice chuyển thành `Paid`.

Lưu ý: online payment hiện là capture nội bộ vì chưa có gateway sandbox bên ngoài.

### Bước 12 — Manual payment

Đăng nhập bằng `FacilityStaff` hoặc `FacilityManager`.

```http
POST /api/invoices/{invoiceId}/payments/manual
```

```json
{
  "amount": 100000,
  "referenceNo": "CASH-DEMO-001"
}
```

Kỳ vọng:

- `200` nếu invoice nằm trong facility scope.
- Payment có method `Cash`, status `Succeeded`.
- Invoice được cập nhật trong cùng transaction.

## 4. Các case bắt buộc phải test lỗi

### Validation

- Quote với `rentalMonths=0` hoặc số âm.
- Unit Type có code rỗng hoặc dài hơn 10 ký tự.
- Giá âm hoặc discount lớn hơn 100%.
- Amount payment bằng 0 hoặc âm.

Kỳ vọng: `400`.

### Authorization

- Customer gọi `POST /api/pricing-policies`.
- Staff gọi `POST /api/unit-types`.
- Customer gọi manual payment.
- Staff gọi online payment.

Kỳ vọng: `401` hoặc `403` tùy trạng thái token.

### Business rules

- Quote ở facility không tồn tại hoặc inactive.
- Quote cho unit type chưa có UnitPrice trong policy.
- Activate policy chưa có UnitPrice.
- Thanh toán vượt outstanding amount.
- Thanh toán invoice đã `Paid` hoặc `WrittenOff`.
- Customer lấy invoice của customer khác.

Kỳ vọng: `404` hoặc `409`, không tạo Payment dở dang và không cập nhật sai PaidAmount.

## 5. Checklist demo cuối cùng

- [ ] API chạy tại `http://localhost:5108`.
- [ ] Swagger mở được.
- [ ] Login trả JWT.
- [ ] Swagger Authorize hoạt động.
- [ ] Unit Type list/detail hoạt động.
- [ ] Availability trả số liệu đúng.
- [ ] Quote tính đúng discount/deposit/fee.
- [ ] Pricing policy tạo và activate được.
- [ ] Unit price lưu đúng policy.
- [ ] Invoice list chỉ lộ dữ liệu đúng ownership/scope.
- [ ] Online payment cập nhật invoice.
- [ ] Manual payment cập nhật invoice.
- [ ] Case vượt tiền bị `409`.
- [ ] Sai role bị `403`.
- [ ] `dotnet test StorEase.sln --no-restore` pass.

## 6. Lệnh kiểm tra trước khi trình diễn

```powershell
dotnet build StorEase.sln --no-restore
dotnet test StorEase.sln --no-restore
```

Không chạy migration trong lúc demo. Nếu cần reset dữ liệu, backup database trước và dùng script reset/seed riêng của môi trường demo.
