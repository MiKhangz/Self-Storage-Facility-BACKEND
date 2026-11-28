# StorEase PaymentReceiver (Rust — VietQR + Automatic Reconciliation + Manual Fallback)

An **ACADEMIC DEMO** payment receiver service for StorEase written in Rust (`Cargo` workspace).
- Generates dynamic **EMVCo / NAPAS 247 VietQR** payloads (`QRIBFTTA`) locked to exact integer VND amounts (`i64`) and normalized booking/contract reference codes (`<= 23` chars).
- Configured in demo mode with the provided **TPBank VietQR** receiver account (`BIN: 970423`, Account: `00000285636`, Holder: `BUI DUC THIEN`), overridable via environment variables.
- Reconciles incoming bank transfers automatically (via `MockBankFeed`, HMAC-signed `WebhookFeed` supporting SePay/Casso/Canonical formats, or `CsvImportFeed`).
- Provides role-gated manual payment recording (`FINANCE`, `FACILITY_MANAGER`, `ADMIN`), two-person approval above a configurable VND threshold, immutable reversing entries, and an append-only audit log.

---

## Workspace Crates
1. `crates/vietqr` — Pure library crate (no I/O): TLV builder/parser, CRC-16/CCITT-FALSE (`0x1021`, init `0xFFFF`), Vietnamese diacritic/hyphen normalization, and PNG/SVG QR rendering (`EcLevel::M`).
2. `crates/payments-core` — Domain models, `BankFeed` trait, deterministic reconciliation engine (`evaluate_transaction`), role/permission guards, and callback event payloads (`DepositPaid`, `RentPaid`).
3. `crates/payments-api` — Axum + Tokio + SQLx HTTP service, SSE live status stream, HMAC-SHA256 webhook verification, outbox retry worker, and SQLite/Postgres persistence.

---

## Environment Variables
| Variable | Default (when `DEMO_MODE=true`) | Description |
| :--- | :--- | :--- |
| `DEMO_MODE` | `true` | Enables demo defaults and `/demo/simulate-transfer`. Refuses startup if `false` with placeholder secrets. |
| `BANK_NAME` | `TPBank` | Display name of the receiving bank. |
| `BANK_BIN` | `970423` | 6-digit NAPAS acquirer BIN (`970423` = TPBank). |
| `BANK_ACCOUNT_NO` | `00000285636` | Beneficiary bank account number (digits only; masked in logs as `••••••••636`). |
| `BANK_ACCOUNT_NAME` | `BUI DUC THIEN` | Beneficiary account holder name. |
| `WEBHOOK_SECRET` | `storease-demo-webhook-hmac-secret-2026` | HMAC-SHA256 secret for `POST /webhooks/bank`. |
| `DATABASE_URL` | `sqlite://payments_demo.db?mode=rwc` | SQLx database connection URL. |
| `BIND_ADDR` | `0.0.0.0:4100` | HTTP bind address. |
| `BOOKING_CALLBACK_URL` | *(Optional)* | URL on `StorEase.Api` to receive `DepositPaid` / `RentPaid` callbacks. |
| `MAX_DESCRIPTION_LEN` | `23` | Max normalized reference length in Tag `62.08`. |
| `DUAL_APPROVAL_THRESHOLD_VND` | `5000000` | Manual entries above this amount require a second approver (`approved_by != recorded_by`). |

---

## Setup & Running Tests
```bash
cd Self-Storage-Facility-BACKEND/PaymentReceiver

# Run all unit and concurrency tests across vietqr, payments-core, and payments-api
cargo test --workspace

# Start the HTTP server on port 4100
cargo run -p payments-api
```

---

## Demo Script (End-to-End Walkthrough)

### Part A: Automatic Reconciliation via `MockBankFeed`
1. **Create a payment request for reservation `RS-261127-867759` (855,000 VND)**:
   ```bash
   curl -X POST http://localhost:4100/payment-requests \
     -H "Content-Type: application/json" \
     -d '{
       "subject_type": "RESERVATION",
       "subject_id": "RS-261127-867759",
       "purpose": "DEPOSIT",
       "amount_vnd": 855000,
       "expires_at": "2026-12-31T13:30:00Z"
     }'
   ```
   Returns `id`, `reference: "RS261127867759 PAY"`, `qr_payload`, and `qr_svg_url`.
2. **Subscribe to live SSE updates in another terminal**:
   ```bash
   curl -N http://localhost:4100/payment-requests/<REQUEST_ID>/events
   ```
3. **Simulate an incoming bank transfer via `MockBankFeed`**:
   ```bash
   curl -X POST http://localhost:4100/demo/simulate-transfer \
     -H "Content-Type: application/json" \
     -d '{
       "amount_vnd": 855000,
       "description": "IBFT Chuyển tiền đặt cọc RS-261127-867759 PAY"
     }'
   ```
4. **Verify**: The SSE stream immediately emits `"status": "PAID"`, `payment_request` transitions to `PAID`, and a `DepositPaid` callback event is written to `callback_outbox`.

### Part B: Fallback Manual Payment Entry (When Feed Is Down)
1. **Record a manual payment as `FACILITY_MANAGER`**:
   ```bash
   curl -X POST http://localhost:4100/payments/manual \
     -H "Content-Type: application/json" \
     -H "X-Actor-Id: manager@storease.vn" \
     -H "X-Actor-Role: FACILITY_MANAGER" \
     -d '{
       "payment_request_id": "<REQUEST_ID>",
       "amount_vnd": 855000,
       "bank_reference_code": "FT26112899001",
       "transfer_time": "2026-11-28T06:30:00Z",
       "reason": "Webhook feed delayed; verified customer TPBank receipt at counter",
       "evidence": "tpbank_receipt_FT26112899001.jpg",
       "actor_id": "manager@storease.vn",
       "actor_role": "FACILITY_MANAGER"
     }'
   ```
2. If `amount_vnd > DUAL_APPROVAL_THRESHOLD_VND`, the request moves to `UNDER_REVIEW` (`approval_pending = true`) until a second authorized user (`FINANCE` or `ADMIN`, different `X-Actor-Id`) calls `POST /payments/<PAYMENT_ID>/approve`.

---

## How to Verify VietQR Scan with a Real Banking App
1. Open `http://localhost:4100/payment-requests/<REQUEST_ID>/qr.png` in a browser.
2. Open any VietQR-supported banking app (TPBank, Vietcombank, MB, Techcombank, ACB, etc.) and tap **Quét QR (Scan QR)**.
3. Verify the pre-filled fields before confirming (do **not** complete a real transfer unless testing with your own account):
   - **Receiving Bank**: `TPBank` (BIN `970423`)
   - **Account Number**: `0000 0285 636` (`BUI DUC THIEN`)
   - **Amount**: Locked to the exact integer VND amount from Tag `54`
   - **Transfer Description (Nội dung)**: Pre-filled with `<REFERENCE>` (e.g. `RS261127867759 PAY`) from Tag `62.08`

---

## Assumptions & Spec Notes
- **NAPAS VietQR Tag `38` Subtags**: Uses `00` = `A000000727` (NAPAS GUID), `01` = Beneficiary Organization (`00` = 6-digit Bank BIN `970423`, `01` = Account Number `00000285636`), and `02` = `QRIBFTTA` (Interbank Fast Transfer to Account). Account holder name (`BUI DUC THIEN`) is resolved dynamically by the scanning banking app via NAPAS account inquiry rather than embedded in Tag `59`, matching the official VietQR personal/merchant IBFT format shown in the TPBank VietQR card.
- **Expiry Race Grace Window**: Configured to `30 seconds` (`EXPIRY_RACE_GRACE_SECONDS = 30`) so a bank transfer initiated just before `expires_at` that settles a few seconds later still reconciles cleanly, while transfers after the grace window enter `UNDER_REVIEW` (`late payment`).
