//! StorEase `payments-api` — Axum HTTP service for VietQR generation, SSE live payment updates,
//! HMAC-signed bank webhooks, MockBankFeed simulator, automatic reconciliation, and dual-control manual entry.

pub mod config;
pub mod feeds;

use std::{
    collections::HashMap,
    convert::Infallible,
    sync::Arc,
    time::Duration as StdDuration,
};

use axum::{
    body::Bytes,
    extract::{Path, Query, State},
    http::{header, HeaderMap, StatusCode},
    response::{
        sse::{Event, KeepAlive, Sse},
        IntoResponse, Response,
    },
    routing::{get, post},
    Json, Router,
};
use chrono::{DateTime, Utc};
use config::AppConfig;
use feeds::{CsvImportFeed, MockBankFeed, WebhookPayload};
use futures::stream::Stream;
use hmac::{Hmac, Mac};
use payments_core::{
    evaluate_transaction, validate_manual_approval, validate_manual_entry, BankFeed,
    BankTransaction, BookingCallbackEvent, Cursor, IncomingTxn, ManualPaymentInput, MatchStatus,
    PaymentPurpose, PaymentRecord, PaymentRequest, PaymentRequestStatus, PaymentSource,
    ReconciliationDecision, StaffRole, SubjectType,
};
use serde::{Deserialize, Serialize};
use sha2::Sha256;
use sqlx::{Row, SqlitePool};
use tokio::sync::{broadcast, Mutex};
use tokio_stream::{wrappers::BroadcastStream, StreamExt};
use tower_http::cors::{Any, CorsLayer};
use tracing::{info, warn};
use uuid::Uuid;
use vietqr::{build_payload_with_max_len, build_reference, normalize_bank_description, normalize_code, render_png, render_svg};

type HmacSha256 = Hmac<Sha256>;

#[derive(Clone)]
pub struct AppState {
    pub db: SqlitePool,
    pub config: AppConfig,
    pub mock_feed: MockBankFeed,
    pub sse_channels: Arc<Mutex<HashMap<Uuid, broadcast::Sender<PaymentSseEvent>>>>,
    /// Single-writer reconciliation mutex ensuring SQLite transaction serialization under concurrent webhooks.
    pub reconcile_lock: Arc<Mutex<()>>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct PaymentSseEvent {
    pub payment_request_id: Uuid,
    pub status: PaymentRequestStatus,
    pub amount_vnd: i64,
    pub reference: String,
    pub paid_at: Option<DateTime<Utc>>,
}

#[derive(Debug, Deserialize)]
pub struct CreatePaymentRequestInput {
    pub subject_type: SubjectType,
    pub subject_id: String,
    pub purpose: PaymentPurpose,
    pub amount_vnd: i64,
    pub expires_at: DateTime<Utc>,
    pub keyword: Option<String>,
}

#[derive(Debug, Serialize)]
pub struct PaymentRequestResponse {
    pub id: Uuid,
    pub subject_type: SubjectType,
    pub subject_id: String,
    pub purpose: PaymentPurpose,
    pub amount_vnd: i64,
    pub status: PaymentRequestStatus,
    pub reference: String,
    pub bank_name: String,
    pub bank_bin: String,
    pub account_no: String,
    pub account_name: String,
    pub qr_payload: String,
    pub qr_png_url: String,
    pub qr_svg_url: String,
    pub events_sse_url: String,
    pub expires_at: DateTime<Utc>,
    pub created_at: DateTime<Utc>,
    pub paid_at: Option<DateTime<Utc>>,
}

#[derive(Debug, Deserialize)]
pub struct SimulateTransferInput {
    pub amount_vnd: i64,
    pub description: String,
}

#[derive(Debug, Deserialize)]
pub struct AssignTransactionInput {
    pub payment_request_id: Uuid,
    pub reason: String,
}

#[derive(Debug, Deserialize)]
pub struct ApproveManualInput {
    pub reason: Option<String>,
}

#[derive(Debug, Deserialize)]
pub struct ReverseManualInput {
    pub reason: String,
    pub evidence: String,
}

#[derive(Debug, Deserialize)]
pub struct QrSizeQuery {
    pub size: Option<u32>,
}

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    tracing_subscriber::fmt()
        .with_env_filter(
            tracing_subscriber::EnvFilter::try_from_default_env()
                .unwrap_or_else(|_| "payments_api=info,tower_http=info".into()),
        )
        .init();

    let config = AppConfig::from_env().expect("Failed to load AppConfig");
    info!(
        bank = %config.bank_name,
        bin = %config.bank_bin,
        account = %config.masked_account_no(),
        holder = %config.bank_account_name,
        demo_mode = config.demo_mode,
        "Starting StorEase PaymentReceiver (VietQR)"
    );

    let db = SqlitePool::connect(&config.database_url).await?;
    sqlx::query(include_str!("../../../migrations/0001_initial.sql"))
        .execute(&db)
        .await?;

    let state = AppState {
        db,
        config: config.clone(),
        mock_feed: MockBankFeed::new(),
        sse_channels: Arc::new(Mutex::new(HashMap::new())),
        reconcile_lock: Arc::new(Mutex::new(())),
    };

    // Background worker: expire stale PENDING requests, retry callbacks, and alert on old UNDER_REVIEW items
    let worker_state = state.clone();
    tokio::spawn(async move {
        let mut interval = tokio::time::interval(StdDuration::from_secs(15));
        loop {
            interval.tick().await;
            let _ = expire_overdue_requests(&worker_state).await;
            let _ = process_callback_outbox(&worker_state).await;
        }
    });

    let app = build_router(state);
    let listener = tokio::net::TcpListener::bind(&config.bind_addr).await?;
    info!(addr = %config.bind_addr, "Listening for HTTP requests");
    axum::serve(listener, app).await?;
    Ok(())
}

pub fn build_router(state: AppState) -> Router {
    let cors = CorsLayer::new()
        .allow_origin(Any)
        .allow_methods(Any)
        .allow_headers(Any);

    Router::new()
        .route("/health", get(health_handler))
        .route("/payment-requests", post(create_payment_request))
        .route("/payment-requests/:id", get(get_payment_request))
        .route("/payment-requests/:id/events", get(payment_request_events_sse))
        .route("/payment-requests/:id/qr.png", get(get_qr_png))
        .route("/payment-requests/:id/qr.svg", get(get_qr_svg))
        .route("/payment-requests/:id/cancel", post(cancel_payment_request))
        .route("/webhooks/bank", post(bank_webhook_handler))
        .route("/demo/simulate-transfer", post(simulate_transfer_handler))
        .route("/demo/import-csv", post(import_csv_handler))
        .route("/payments/manual", post(record_manual_payment))
        .route("/payments/:id/approve", post(approve_manual_payment))
        .route("/payments/:id/reverse", post(reverse_manual_payment))
        .route("/transactions/unmatched", get(list_unmatched_transactions))
        .route("/transactions/:id/assign", post(assign_unmatched_transaction))
        .layer(cors)
        .with_state(state)
}

async fn health_handler(State(state): State<AppState>) -> Json<serde_json::Value> {
    Json(serde_json::json!({
        "status": "ok",
        "demo_mode": state.config.demo_mode,
        "bank_name": state.config.bank_name,
        "bank_bin": state.config.bank_bin,
        "account_masked": state.config.masked_account_no(),
        "account_name": state.config.bank_account_name,
    }))
}

/// `POST /payment-requests` — Idempotent per `(subject_type, subject_id, purpose)` while `PENDING`.
async fn create_payment_request(
    State(state): State<AppState>,
    Json(input): Json<CreatePaymentRequestInput>,
) -> Result<(StatusCode, Json<PaymentRequestResponse>), (StatusCode, Json<serde_json::Value>)> {
    if input.amount_vnd <= 0 {
        return Err(json_err(
            StatusCode::BAD_REQUEST,
            "amount_vnd must be a positive integer VND",
        ));
    }

    // Check existing PENDING request for idempotency
    let existing = sqlx::query(
        "SELECT id, subject_type, subject_id, reference, code_token, purpose, amount_vnd, status, qr_payload, expires_at, created_at, paid_at
         FROM payment_request
         WHERE subject_type = ? AND subject_id = ? AND purpose = ? AND status = 'PENDING'
         LIMIT 1",
    )
    .bind(input.subject_type.as_str())
    .bind(&input.subject_id)
    .bind(input.purpose.as_str())
    .fetch_optional(&state.db)
    .await
    .map_err(|e| json_err(StatusCode::INTERNAL_SERVER_ERROR, &e.to_string()))?;

    if let Some(row) = existing {
        let req = row_to_payment_request(&row);
        return Ok((StatusCode::OK, Json(to_response(&req, &state.config))));
    }

    let kw = input
        .keyword
        .as_deref()
        .unwrap_or_else(|| input.purpose.default_keyword());
    let code_token = normalize_code(&input.subject_id);
    let reference = build_reference(&input.subject_id, kw, state.config.max_description_len)
        .map_err(|e| json_err(StatusCode::BAD_REQUEST, &e.to_string()))?;

    let qr_payload = build_payload_with_max_len(
        &state.config.bank_bin,
        &state.config.bank_account_no,
        input.amount_vnd,
        &reference,
        state.config.max_description_len,
    )
    .map_err(|e| json_err(StatusCode::BAD_REQUEST, &e.to_string()))?;

    let id = Uuid::new_v4();
    let now = Utc::now();

    sqlx::query(
        "INSERT INTO payment_request (id, subject_type, subject_id, reference, code_token, purpose, amount_vnd, status, qr_payload, expires_at, created_at, paid_at)
         VALUES (?, ?, ?, ?, ?, ?, ?, 'PENDING', ?, ?, ?, NULL)",
    )
    .bind(id.to_string())
    .bind(input.subject_type.as_str())
    .bind(&input.subject_id)
    .bind(&reference)
    .bind(&code_token)
    .bind(input.purpose.as_str())
    .bind(input.amount_vnd)
    .bind(&qr_payload)
    .bind(input.expires_at.to_rfc3339())
    .bind(now.to_rfc3339())
    .execute(&state.db)
    .await
    .map_err(|e| json_err(StatusCode::INTERNAL_SERVER_ERROR, &e.to_string()))?;

    append_audit_log(
        &state.db,
        "payment_request",
        &id.to_string(),
        "CREATED",
        "SYSTEM",
        None,
        None,
        None,
        Some("PENDING"),
        Some(&format!("reference={reference}, amount_vnd={}", input.amount_vnd)),
    )
    .await;

    let req = PaymentRequest {
        id,
        subject_type: input.subject_type,
        subject_id: input.subject_id,
        reference,
        code_token,
        purpose: input.purpose,
        amount_vnd: input.amount_vnd,
        status: PaymentRequestStatus::Pending,
        qr_payload,
        expires_at: input.expires_at,
        created_at: now,
        paid_at: None,
    };

    Ok((StatusCode::CREATED, Json(to_response(&req, &state.config))))
}

async fn get_payment_request(
    State(state): State<AppState>,
    Path(id): Path<Uuid>,
) -> Result<Json<PaymentRequestResponse>, (StatusCode, Json<serde_json::Value>)> {
    let req = load_payment_request(&state.db, id)
        .await
        .ok_or_else(|| json_err(StatusCode::NOT_FOUND, "payment request not found"))?;
    Ok(Json(to_response(&req, &state.config)))
}

async fn payment_request_events_sse(
    State(state): State<AppState>,
    Path(id): Path<Uuid>,
) -> Sse<impl Stream<Item = Result<Event, Infallible>>> {
    let rx = {
        let mut channels = state.sse_channels.lock().await;
        let tx = channels
            .entry(id)
            .or_insert_with(|| broadcast::channel(16).0);
        tx.subscribe()
    };

    let stream = BroadcastStream::new(rx).filter_map(|msg| match msg {
        Ok(ev) => {
            let json = serde_json::to_string(&ev).unwrap_or_default();
            Some(Ok(Event::default().event("payment_status").data(json)))
        }
        Err(_) => None,
    });

    Sse::new(stream).keep_alive(KeepAlive::default())
}

async fn get_qr_png(
    State(state): State<AppState>,
    Path(id): Path<Uuid>,
    Query(q): Query<QrSizeQuery>,
) -> Result<Response, (StatusCode, Json<serde_json::Value>)> {
    let req = load_payment_request(&state.db, id)
        .await
        .ok_or_else(|| json_err(StatusCode::NOT_FOUND, "payment request not found"))?;
    let bytes = render_png(&req.qr_payload, q.size.unwrap_or(360))
        .map_err(|e| json_err(StatusCode::INTERNAL_SERVER_ERROR, &e.to_string()))?;
    Ok(([(header::CONTENT_TYPE, "image/png")], bytes).into_response())
}

async fn get_qr_svg(
    State(state): State<AppState>,
    Path(id): Path<Uuid>,
) -> Result<Response, (StatusCode, Json<serde_json::Value>)> {
    let req = load_payment_request(&state.db, id)
        .await
        .ok_or_else(|| json_err(StatusCode::NOT_FOUND, "payment request not found"))?;
    let svg = render_svg(&req.qr_payload)
        .map_err(|e| json_err(StatusCode::INTERNAL_SERVER_ERROR, &e.to_string()))?;
    Ok(([(header::CONTENT_TYPE, "image/svg+xml")], svg).into_response())
}

async fn cancel_payment_request(
    State(state): State<AppState>,
    Path(id): Path<Uuid>,
) -> Result<Json<PaymentRequestResponse>, (StatusCode, Json<serde_json::Value>)> {
    let mut req = load_payment_request(&state.db, id)
        .await
        .ok_or_else(|| json_err(StatusCode::NOT_FOUND, "payment request not found"))?;
    if req.status != PaymentRequestStatus::Pending {
        return Err(json_err(
            StatusCode::CONFLICT,
            &format!("only PENDING requests can be cancelled; current status is {}", req.status.as_str()),
        ));
    }

    sqlx::query("UPDATE payment_request SET status = 'CANCELLED' WHERE id = ?")
        .bind(id.to_string())
        .execute(&state.db)
        .await
        .map_err(|e| json_err(StatusCode::INTERNAL_SERVER_ERROR, &e.to_string()))?;

    append_audit_log(
        &state.db,
        "payment_request",
        &id.to_string(),
        "CANCELLED",
        "USER",
        None,
        None,
        Some("PENDING"),
        Some("CANCELLED"),
        Some("Cancelled via API"),
    )
    .await;

    req.status = PaymentRequestStatus::Cancelled;
    notify_sse(&state, &req).await;
    Ok(Json(to_response(&req, &state.config)))
}

/// `POST /webhooks/bank` — Verifies HMAC-SHA256 (`X-Signature`) and timestamp tolerance (`X-Timestamp`, 5 minutes),
/// maps SePay / Casso / canonical payloads, and runs the deterministic reconciliation engine.
async fn bank_webhook_handler(
    State(state): State<AppState>,
    headers: HeaderMap,
    body: Bytes,
) -> Result<Json<serde_json::Value>, (StatusCode, Json<serde_json::Value>)> {
    verify_webhook_hmac(&state.config.webhook_secret, &headers, &body)?;

    let raw_str = std::str::from_utf8(&body)
        .map_err(|_| json_err(StatusCode::BAD_REQUEST, "invalid UTF-8 body"))?;
    let payload: WebhookPayload = serde_json::from_slice(&body)
        .map_err(|e| json_err(StatusCode::BAD_REQUEST, &format!("invalid webhook payload: {e}")))?;

    let incoming_list = payload.into_incoming_txns(raw_str);
    let mut results = Vec::new();
    for txn in incoming_list {
        let decision = reconcile_incoming_txn(&state, txn).await?;
        results.push(decision);
    }

    Ok(Json(serde_json::json!({
        "processed": results.len(),
        "decisions": results,
    })))
}

/// `POST /demo/simulate-transfer` — Pushes a simulated bank transfer into `MockBankFeed`, drains it via `BankFeed::fetch_new`,
/// and runs the exact same reconciliation engine.
async fn simulate_transfer_handler(
    State(state): State<AppState>,
    Json(input): Json<SimulateTransferInput>,
) -> Result<Json<serde_json::Value>, (StatusCode, Json<serde_json::Value>)> {
    if !state.config.demo_mode {
        return Err(json_err(
            StatusCode::FORBIDDEN,
            "MockBankFeed simulation is only enabled when DEMO_MODE=true",
        ));
    }
    state
        .mock_feed
        .push_simulated_transfer(input.amount_vnd, input.description);
    let txns = state
        .mock_feed
        .fetch_new(Cursor::default())
        .await
        .map_err(|e| json_err(StatusCode::INTERNAL_SERVER_ERROR, &e.to_string()))?;

    let mut decisions = Vec::new();
    for txn in txns {
        decisions.push(reconcile_incoming_txn(&state, txn).await?);
    }
    Ok(Json(serde_json::json!({
        "simulated": decisions.len(),
        "decisions": decisions,
    })))
}

async fn import_csv_handler(
    State(state): State<AppState>,
    body: String,
) -> Result<Json<serde_json::Value>, (StatusCode, Json<serde_json::Value>)> {
    let feed = CsvImportFeed::new(body);
    let txns = feed
        .fetch_new(Cursor::default())
        .await
        .map_err(|e| json_err(StatusCode::BAD_REQUEST, &e.to_string()))?;
    let mut decisions = Vec::new();
    for txn in txns {
        decisions.push(reconcile_incoming_txn(&state, txn).await?);
    }
    Ok(Json(serde_json::json!({
        "imported": decisions.len(),
        "decisions": decisions,
    })))
}

/// Core transactional reconciliation execution (serialized via `reconcile_lock` + DB transaction).
pub async fn reconcile_incoming_txn(
    state: &AppState,
    incoming: IncomingTxn,
) -> Result<ReconciliationDecision, (StatusCode, Json<serde_json::Value>)> {
    let _guard = state.reconcile_lock.lock().await;

    // 1. Check duplicate provider_txn_id
    let existing_txn = sqlx::query("SELECT id FROM bank_transaction WHERE provider_txn_id = ? LIMIT 1")
        .bind(&incoming.provider_txn_id)
        .fetch_optional(&state.db)
        .await
        .map_err(|e| json_err(StatusCode::INTERNAL_SERVER_ERROR, &e.to_string()))?;

    let existing_txn_uuid = existing_txn
        .and_then(|r| r.try_get::<String, _>("id").ok())
        .and_then(|s| Uuid::parse_str(&s).ok());

    // 2. Check if a manual payment already recorded this bank reference
    let manual_row = sqlx::query(
        "SELECT id, payment_request_id, amount_vnd, recorded_by, approved_by, approval_pending, created_at
         FROM payment
         WHERE source = 'MANUAL' AND provider_txn_ref = ? AND reversal_of_payment_id IS NULL
         LIMIT 1",
    )
    .bind(&incoming.provider_txn_id)
    .fetch_optional(&state.db)
    .await
    .map_err(|e| json_err(StatusCode::INTERNAL_SERVER_ERROR, &e.to_string()))?;

    let manual_record = manual_row.map(|r| PaymentRecord {
        id: Uuid::parse_str(&r.get::<String, _>("id")).unwrap_or_default(),
        payment_request_id: Uuid::parse_str(&r.get::<String, _>("payment_request_id")).unwrap_or_default(),
        bank_transaction_id: None,
        provider_txn_ref: Some(incoming.provider_txn_id.clone()),
        source: PaymentSource::Manual,
        amount_vnd: r.get("amount_vnd"),
        recorded_by: r.get("recorded_by"),
        approved_by: r.get("approved_by"),
        approval_pending: r.get::<i64, _>("approval_pending") != 0,
        reversal_of_payment_id: None,
        reason: None,
        evidence: None,
        created_at: Utc::now(),
    });

    // 3. Load candidate payment requests
    let candidate_rows = sqlx::query(
        "SELECT id, subject_type, subject_id, reference, code_token, purpose, amount_vnd, status, qr_payload, expires_at, created_at, paid_at
         FROM payment_request
         ORDER BY created_at DESC
         LIMIT 200",
    )
    .fetch_all(&state.db)
    .await
    .map_err(|e| json_err(StatusCode::INTERNAL_SERVER_ERROR, &e.to_string()))?;

    let candidates: Vec<PaymentRequest> = candidate_rows.iter().map(row_to_payment_request).collect();
    let now = Utc::now();

    let decision = evaluate_transaction(
        &incoming,
        existing_txn_uuid,
        manual_record.as_ref(),
        &candidates,
        now,
    );

    if let ReconciliationDecision::DuplicateTransaction { .. } = decision {
        append_audit_log(
            &state.db,
            "bank_transaction",
            &incoming.provider_txn_id,
            "DUPLICATE_IGNORED",
            &incoming.provider,
            None,
            None,
            None,
            Some("DUPLICATE"),
            Some("Duplicate provider_txn_id ignored"),
        )
        .await;
        return Ok(decision);
    }

    let txn_uuid = Uuid::new_v4();
    let norm_desc = normalize_bank_description(&incoming.raw_description);
    let match_status = match &decision {
        ReconciliationDecision::AutoConfirmedPaid { .. } => MatchStatus::Matched,
        ReconciliationDecision::LinkedToManualPayment { amount_matches: true, .. } => {
            MatchStatus::Matched
        }
        ReconciliationDecision::LinkedToManualPayment { amount_matches: false, .. }
        | ReconciliationDecision::UnderReviewAmountMismatch { .. }
        | ReconciliationDecision::UnderReviewLatePayment { .. }
        | ReconciliationDecision::UnderReviewDuplicatePayment { .. } => MatchStatus::Review,
        ReconciliationDecision::Unmatched | ReconciliationDecision::IgnoredDebit => {
            MatchStatus::Unmatched
        }
        ReconciliationDecision::DuplicateTransaction { .. } => MatchStatus::Duplicate,
    };

    sqlx::query(
        "INSERT INTO bank_transaction (id, provider, provider_txn_id, amount_vnd, is_credit, raw_description, normalized_description, occurred_at, received_at, raw_json, match_status)
         VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
    )
    .bind(txn_uuid.to_string())
    .bind(&incoming.provider)
    .bind(&incoming.provider_txn_id)
    .bind(incoming.amount_vnd)
    .bind(if incoming.is_credit { 1 } else { 0 })
    .bind(&incoming.raw_description)
    .bind(&norm_desc)
    .bind(incoming.occurred_at.to_rfc3339())
    .bind(now.to_rfc3339())
    .bind(&incoming.raw_json)
    .bind(match_status.as_str())
    .execute(&state.db)
    .await
    .map_err(|e| json_err(StatusCode::INTERNAL_SERVER_ERROR, &e.to_string()))?;

    match &decision {
        ReconciliationDecision::AutoConfirmedPaid {
            payment_request_id,
            amount_vnd,
        } => {
            let payment_id = Uuid::new_v4();
            sqlx::query(
                "UPDATE payment_request SET status = 'PAID', paid_at = ? WHERE id = ?",
            )
            .bind(now.to_rfc3339())
            .bind(payment_request_id.to_string())
            .execute(&state.db)
            .await
            .map_err(|e| json_err(StatusCode::INTERNAL_SERVER_ERROR, &e.to_string()))?;

            sqlx::query(
                "INSERT INTO payment (id, payment_request_id, bank_transaction_id, provider_txn_ref, source, amount_vnd, recorded_by, approved_by, approval_pending, reversal_of_payment_id, reason, evidence, created_at)
                 VALUES (?, ?, ?, ?, 'AUTO', ?, NULL, NULL, 0, NULL, 'Auto-reconciled bank transfer', NULL, ?)",
            )
            .bind(payment_id.to_string())
            .bind(payment_request_id.to_string())
            .bind(txn_uuid.to_string())
            .bind(&incoming.provider_txn_id)
            .bind(*amount_vnd)
            .bind(now.to_rfc3339())
            .execute(&state.db)
            .await
            .map_err(|e| json_err(StatusCode::INTERNAL_SERVER_ERROR, &e.to_string()))?;

            append_audit_log(
                &state.db,
                "payment_request",
                &payment_request_id.to_string(),
                "AUTO_PAID",
                &incoming.provider,
                None,
                None,
                Some("PENDING"),
                Some("PAID"),
                Some(&format!("Matched bank_transaction {txn_uuid}")),
            )
            .await;

            if let Some(updated_req) = load_payment_request(&state.db, *payment_request_id).await {
                enqueue_booking_callback(&state.db, &updated_req, payment_id, PaymentSource::Auto).await;
                notify_sse(state, &updated_req).await;
            }
        }
        ReconciliationDecision::LinkedToManualPayment {
            payment_id,
            payment_request_id,
            amount_matches,
        } => {
            sqlx::query("UPDATE payment SET bank_transaction_id = ? WHERE id = ?")
                .bind(txn_uuid.to_string())
                .bind(payment_id.to_string())
                .execute(&state.db)
                .await
                .map_err(|e| json_err(StatusCode::INTERNAL_SERVER_ERROR, &e.to_string()))?;

            if !amount_matches {
                sqlx::query("UPDATE payment_request SET status = 'UNDER_REVIEW' WHERE id = ?")
                    .bind(payment_request_id.to_string())
                    .execute(&state.db)
                    .await
                    .ok();
            }
            append_audit_log(
                &state.db,
                "payment",
                &payment_id.to_string(),
                "LINKED_BANK_TXN",
                &incoming.provider,
                None,
                None,
                None,
                Some(if *amount_matches { "LINKED" } else { "UNDER_REVIEW" }),
                Some(&format!("Linked late feed transaction {txn_uuid} to manual payment")),
            )
            .await;
        }
        ReconciliationDecision::UnderReviewAmountMismatch {
            payment_request_id,
            reason,
            ..
        }
        | ReconciliationDecision::UnderReviewLatePayment {
            payment_request_id,
            reason,
            ..
        }
        | ReconciliationDecision::UnderReviewDuplicatePayment {
            payment_request_id,
            reason,
        } => {
            sqlx::query(
                "UPDATE payment_request SET status = 'UNDER_REVIEW' WHERE id = ? AND status != 'PAID'",
            )
            .bind(payment_request_id.to_string())
            .execute(&state.db)
            .await
            .ok();

            append_audit_log(
                &state.db,
                "payment_request",
                &payment_request_id.to_string(),
                "FLAGGED_UNDER_REVIEW",
                &incoming.provider,
                None,
                None,
                None,
                Some("UNDER_REVIEW"),
                Some(reason),
            )
            .await;

            if let Some(updated_req) = load_payment_request(&state.db, *payment_request_id).await {
                notify_sse(state, &updated_req).await;
            }
        }
        ReconciliationDecision::Unmatched | ReconciliationDecision::IgnoredDebit => {
            append_audit_log(
                &state.db,
                "bank_transaction",
                &txn_uuid.to_string(),
                "UNMATCHED",
                &incoming.provider,
                None,
                None,
                None,
                Some("UNMATCHED"),
                Some(&incoming.raw_description),
            )
            .await;
        }
        ReconciliationDecision::DuplicateTransaction { .. } => {}
    }

    Ok(decision)
}

/// `POST /payments/manual` — Role-restricted manual fallback entry with dual-approval threshold and audit logging.
async fn record_manual_payment(
    State(state): State<AppState>,
    headers: HeaderMap,
    Json(mut input): Json<ManualPaymentInput>,
) -> Result<(StatusCode, Json<PaymentRecord>), (StatusCode, Json<serde_json::Value>)> {
    let (header_user, header_role, header_ip) = extract_actor(&headers);
    if !header_user.is_empty() {
        input.actor_id = header_user;
    }
    if let Some(r) = header_role {
        input.actor_role = r;
    }
    if input.actor_ip.is_none() {
        input.actor_ip = header_ip;
    }

    let req = load_payment_request(&state.db, input.payment_request_id)
        .await
        .ok_or_else(|| json_err(StatusCode::NOT_FOUND, "payment request not found"))?;

    let needs_second_approver = validate_manual_entry(
        &input,
        req.amount_vnd,
        state.config.dual_approval_threshold_vnd,
    )
    .map_err(|e| {
        let code = match e {
            payments_core::PaymentError::Forbidden(_, _) => StatusCode::FORBIDDEN,
            _ => StatusCode::BAD_REQUEST,
        };
        json_err(code, &e.to_string())
    })?;

    // Check if bank_transaction with same provider_txn_id already arrived in the UNMATCHED queue
    let existing_bank_txn = sqlx::query(
        "SELECT id FROM bank_transaction WHERE provider_txn_id = ? LIMIT 1",
    )
    .bind(input.bank_reference_code.trim())
    .fetch_optional(&state.db)
    .await
    .ok()
    .flatten()
    .and_then(|r| r.try_get::<String, _>("id").ok())
    .and_then(|s| Uuid::parse_str(&s).ok());

    let payment_id = Uuid::new_v4();
    let now = Utc::now();

    sqlx::query(
        "INSERT INTO payment (id, payment_request_id, bank_transaction_id, provider_txn_ref, source, amount_vnd, recorded_by, approved_by, approval_pending, reversal_of_payment_id, reason, evidence, created_at)
         VALUES (?, ?, ?, ?, 'MANUAL', ?, ?, ?, ?, NULL, ?, ?, ?)",
    )
    .bind(payment_id.to_string())
    .bind(req.id.to_string())
    .bind(existing_bank_txn.map(|u| u.to_string()))
    .bind(input.bank_reference_code.trim())
    .bind(input.amount_vnd)
    .bind(&input.actor_id)
    .bind(if needs_second_approver {
        None
    } else {
        Some(input.actor_id.clone())
    })
    .bind(if needs_second_approver { 1 } else { 0 })
    .bind(input.reason.trim())
    .bind(input.evidence.trim())
    .bind(now.to_rfc3339())
    .execute(&state.db)
    .await
    .map_err(|e| json_err(StatusCode::INTERNAL_SERVER_ERROR, &e.to_string()))?;

    let next_status = if needs_second_approver {
        PaymentRequestStatus::UnderReview
    } else {
        PaymentRequestStatus::Paid
    };

    sqlx::query("UPDATE payment_request SET status = ?, paid_at = ? WHERE id = ?")
        .bind(next_status.as_str())
        .bind(if next_status == PaymentRequestStatus::Paid {
            Some(now.to_rfc3339())
        } else {
            None
        })
        .bind(req.id.to_string())
        .execute(&state.db)
        .await
        .map_err(|e| json_err(StatusCode::INTERNAL_SERVER_ERROR, &e.to_string()))?;

    append_audit_log(
        &state.db,
        "payment",
        &payment_id.to_string(),
        if needs_second_approver {
            "MANUAL_RECORDED_AWAITING_APPROVAL"
        } else {
            "MANUAL_RECORDED_PAID"
        },
        &input.actor_id,
        Some(&format!("{:?}", input.actor_role)),
        input.actor_ip.as_deref(),
        Some(req.status.as_str()),
        Some(next_status.as_str()),
        Some(&input.reason),
    )
    .await;

    if next_status == PaymentRequestStatus::Paid {
        if let Some(updated_req) = load_payment_request(&state.db, req.id).await {
            enqueue_booking_callback(&state.db, &updated_req, payment_id, PaymentSource::Manual).await;
            notify_sse(&state, &updated_req).await;
        }
    }

    let record = PaymentRecord {
        id: payment_id,
        payment_request_id: req.id,
        bank_transaction_id: existing_bank_txn,
        provider_txn_ref: Some(input.bank_reference_code.trim().to_string()),
        source: PaymentSource::Manual,
        amount_vnd: input.amount_vnd,
        recorded_by: Some(input.actor_id),
        approved_by: None,
        approval_pending: needs_second_approver,
        reversal_of_payment_id: None,
        reason: Some(input.reason),
        evidence: Some(input.evidence),
        created_at: now,
    };

    Ok((StatusCode::CREATED, Json(record)))
}

/// `POST /payments/:id/approve` — Second-person approval for manual entries above the threshold.
async fn approve_manual_payment(
    State(state): State<AppState>,
    Path(payment_id): Path<Uuid>,
    headers: HeaderMap,
    Json(input): Json<ApproveManualInput>,
) -> Result<Json<PaymentRecord>, (StatusCode, Json<serde_json::Value>)> {
    let (approver_id, approver_role_opt, approver_ip) = extract_actor(&headers);
    let approver_role = approver_role_opt.unwrap_or(StaffRole::Customer);

    let mut payment = load_payment_record(&state.db, payment_id)
        .await
        .ok_or_else(|| json_err(StatusCode::NOT_FOUND, "payment not found"))?;

    validate_manual_approval(&payment, &approver_id, approver_role).map_err(|e| {
        let status = match e {
            payments_core::PaymentError::Forbidden(_, _)
            | payments_core::PaymentError::SelfApprovalForbidden(_) => StatusCode::FORBIDDEN,
            _ => StatusCode::BAD_REQUEST,
        };
        json_err(status, &e.to_string())
    })?;

    let now = Utc::now();
    sqlx::query("UPDATE payment SET approved_by = ?, approval_pending = 0 WHERE id = ?")
        .bind(&approver_id)
        .bind(payment_id.to_string())
        .execute(&state.db)
        .await
        .map_err(|e| json_err(StatusCode::INTERNAL_SERVER_ERROR, &e.to_string()))?;

    sqlx::query("UPDATE payment_request SET status = 'PAID', paid_at = ? WHERE id = ?")
        .bind(now.to_rfc3339())
        .bind(payment.payment_request_id.to_string())
        .execute(&state.db)
        .await
        .map_err(|e| json_err(StatusCode::INTERNAL_SERVER_ERROR, &e.to_string()))?;

    append_audit_log(
        &state.db,
        "payment",
        &payment_id.to_string(),
        "MANUAL_APPROVED",
        &approver_id,
        Some(&format!("{approver_role:?}")),
        approver_ip.as_deref(),
        Some("UNDER_REVIEW"),
        Some("PAID"),
        input.reason.as_deref(),
    )
    .await;

    payment.approved_by = Some(approver_id);
    payment.approval_pending = false;

    if let Some(updated_req) = load_payment_request(&state.db, payment.payment_request_id).await {
        enqueue_booking_callback(&state.db, &updated_req, payment.id, PaymentSource::Manual).await;
        notify_sse(&state, &updated_req).await;
    }

    Ok(Json(payment))
}

/// `POST /payments/:id/reverse` — Creates an immutable reversing payment entry (`-amount_vnd`) referencing the original.
async fn reverse_manual_payment(
    State(state): State<AppState>,
    Path(payment_id): Path<Uuid>,
    headers: HeaderMap,
    Json(input): Json<ReverseManualInput>,
) -> Result<(StatusCode, Json<PaymentRecord>), (StatusCode, Json<serde_json::Value>)> {
    let (actor_id, actor_role_opt, actor_ip) = extract_actor(&headers);
    let actor_role = actor_role_opt.unwrap_or(StaffRole::Customer);
    if !actor_role.can_record_manual() {
        return Err(json_err(
            StatusCode::FORBIDDEN,
            "role lacks payments.record_manual permission",
        ));
    }

    let original = load_payment_record(&state.db, payment_id)
        .await
        .ok_or_else(|| json_err(StatusCode::NOT_FOUND, "original payment not found"))?;

    let reversal_id = Uuid::new_v4();
    let now = Utc::now();
    let neg_amount = -original.amount_vnd;

    sqlx::query(
        "INSERT INTO payment (id, payment_request_id, bank_transaction_id, provider_txn_ref, source, amount_vnd, recorded_by, approved_by, approval_pending, reversal_of_payment_id, reason, evidence, created_at)
         VALUES (?, ?, NULL, ?, 'MANUAL', ?, ?, ?, 0, ?, ?, ?, ?)",
    )
    .bind(reversal_id.to_string())
    .bind(original.payment_request_id.to_string())
    .bind(original.provider_txn_ref.as_deref())
    .bind(neg_amount)
    .bind(&actor_id)
    .bind(&actor_id)
    .bind(original.id.to_string())
    .bind(input.reason.trim())
    .bind(input.evidence.trim())
    .bind(now.to_rfc3339())
    .execute(&state.db)
    .await
    .map_err(|e| json_err(StatusCode::INTERNAL_SERVER_ERROR, &e.to_string()))?;

    sqlx::query("UPDATE payment_request SET status = 'PENDING', paid_at = NULL WHERE id = ?")
        .bind(original.payment_request_id.to_string())
        .execute(&state.db)
        .await
        .ok();

    append_audit_log(
        &state.db,
        "payment",
        &reversal_id.to_string(),
        "MANUAL_REVERSED",
        &actor_id,
        Some(&format!("{actor_role:?}")),
        actor_ip.as_deref(),
        Some("PAID"),
        Some("PENDING"),
        Some(&format!("Reversal of payment {}: {}", original.id, input.reason)),
    )
    .await;

    let reversal = PaymentRecord {
        id: reversal_id,
        payment_request_id: original.payment_request_id,
        bank_transaction_id: None,
        provider_txn_ref: original.provider_txn_ref,
        source: PaymentSource::Manual,
        amount_vnd: neg_amount,
        recorded_by: Some(actor_id.clone()),
        approved_by: Some(actor_id),
        approval_pending: false,
        reversal_of_payment_id: Some(original.id),
        reason: Some(input.reason),
        evidence: Some(input.evidence),
        created_at: now,
    };

    Ok((StatusCode::CREATED, Json(reversal)))
}

async fn list_unmatched_transactions(
    State(state): State<AppState>,
) -> Result<Json<Vec<BankTransaction>>, (StatusCode, Json<serde_json::Value>)> {
    let rows = sqlx::query(
        "SELECT id, provider, provider_txn_id, amount_vnd, is_credit, raw_description, normalized_description, occurred_at, received_at, raw_json, match_status
         FROM bank_transaction
         WHERE match_status IN ('UNMATCHED', 'REVIEW')
         ORDER BY received_at DESC
         LIMIT 100",
    )
    .fetch_all(&state.db)
    .await
    .map_err(|e| json_err(StatusCode::INTERNAL_SERVER_ERROR, &e.to_string()))?;

    let list = rows
        .iter()
        .map(|r| BankTransaction {
            id: Uuid::parse_str(&r.get::<String, _>("id")).unwrap_or_default(),
            provider: r.get("provider"),
            provider_txn_id: r.get("provider_txn_id"),
            amount_vnd: r.get("amount_vnd"),
            is_credit: r.get::<i64, _>("is_credit") != 0,
            raw_description: r.get("raw_description"),
            normalized_description: r.get("normalized_description"),
            occurred_at: DateTime::parse_from_rfc3339(&r.get::<String, _>("occurred_at"))
                .map(|d| d.with_timezone(&Utc))
                .unwrap_or_else(|_| Utc::now()),
            received_at: DateTime::parse_from_rfc3339(&r.get::<String, _>("received_at"))
                .map(|d| d.with_timezone(&Utc))
                .unwrap_or_else(|_| Utc::now()),
            raw_json: r.get("raw_json"),
            match_status: match r.get::<String, _>("match_status").as_str() {
                "MATCHED" => MatchStatus::Matched,
                "REVIEW" => MatchStatus::Review,
                "DUPLICATE" => MatchStatus::Duplicate,
                _ => MatchStatus::Unmatched,
            },
        })
        .collect();

    Ok(Json(list))
}

async fn assign_unmatched_transaction(
    State(state): State<AppState>,
    Path(txn_id): Path<Uuid>,
    headers: HeaderMap,
    Json(input): Json<AssignTransactionInput>,
) -> Result<Json<PaymentRecord>, (StatusCode, Json<serde_json::Value>)> {
    let (actor_id, actor_role_opt, actor_ip) = extract_actor(&headers);
    let actor_role = actor_role_opt.unwrap_or(StaffRole::Customer);
    if !actor_role.can_record_manual() {
        return Err(json_err(
            StatusCode::FORBIDDEN,
            "role lacks permission to assign unmatched transactions",
        ));
    }

    let txn_row = sqlx::query(
        "SELECT provider_txn_id, amount_vnd FROM bank_transaction WHERE id = ? LIMIT 1",
    )
    .bind(txn_id.to_string())
    .fetch_optional(&state.db)
    .await
    .map_err(|e| json_err(StatusCode::INTERNAL_SERVER_ERROR, &e.to_string()))?
    .ok_or_else(|| json_err(StatusCode::NOT_FOUND, "bank transaction not found"))?;

    let provider_txn_id: String = txn_row.get("provider_txn_id");
    let amount_vnd: i64 = txn_row.get("amount_vnd");

    let req = load_payment_request(&state.db, input.payment_request_id)
        .await
        .ok_or_else(|| json_err(StatusCode::NOT_FOUND, "payment request not found"))?;

    if amount_vnd != req.amount_vnd && !actor_role.can_record_partial() {
        return Err(json_err(
            StatusCode::BAD_REQUEST,
            &format!(
                "transaction amount {amount_vnd} does not match request amount {}",
                req.amount_vnd
            ),
        ));
    }

    let payment_id = Uuid::new_v4();
    let now = Utc::now();

    sqlx::query("UPDATE bank_transaction SET match_status = 'MATCHED' WHERE id = ?")
        .bind(txn_id.to_string())
        .execute(&state.db)
        .await
        .ok();

    sqlx::query("UPDATE payment_request SET status = 'PAID', paid_at = ? WHERE id = ?")
        .bind(now.to_rfc3339())
        .bind(req.id.to_string())
        .execute(&state.db)
        .await
        .ok();

    sqlx::query(
        "INSERT INTO payment (id, payment_request_id, bank_transaction_id, provider_txn_ref, source, amount_vnd, recorded_by, approved_by, approval_pending, reversal_of_payment_id, reason, evidence, created_at)
         VALUES (?, ?, ?, ?, 'MANUAL', ?, ?, ?, 0, NULL, ?, 'Assigned from unmatched bank transaction queue', ?)",
    )
    .bind(payment_id.to_string())
    .bind(req.id.to_string())
    .bind(txn_id.to_string())
    .bind(&provider_txn_id)
    .bind(amount_vnd)
    .bind(&actor_id)
    .bind(&actor_id)
    .bind(input.reason.trim())
    .bind(now.to_rfc3339())
    .execute(&state.db)
    .await
    .map_err(|e| json_err(StatusCode::INTERNAL_SERVER_ERROR, &e.to_string()))?;

    append_audit_log(
        &state.db,
        "bank_transaction",
        &txn_id.to_string(),
        "ASSIGNED_TO_REQUEST",
        &actor_id,
        Some(&format!("{actor_role:?}")),
        actor_ip.as_deref(),
        Some("UNMATCHED"),
        Some("MATCHED"),
        Some(&format!("Assigned to request {}: {}", req.id, input.reason)),
    )
    .await;

    if let Some(updated_req) = load_payment_request(&state.db, req.id).await {
        enqueue_booking_callback(&state.db, &updated_req, payment_id, PaymentSource::Manual).await;
        notify_sse(&state, &updated_req).await;
    }

    Ok(Json(PaymentRecord {
        id: payment_id,
        payment_request_id: req.id,
        bank_transaction_id: Some(txn_id),
        provider_txn_ref: Some(provider_txn_id),
        source: PaymentSource::Manual,
        amount_vnd,
        recorded_by: Some(actor_id.clone()),
        approved_by: Some(actor_id),
        approval_pending: false,
        reversal_of_payment_id: None,
        reason: Some(input.reason),
        evidence: Some("Assigned from unmatched bank transaction queue".to_string()),
        created_at: now,
    }))
}

/// Verifies `X-Signature` (hex HMAC-SHA256 over `"<timestamp>.<raw_body>"` or `"<raw_body>"`)
/// and `X-Timestamp` (epoch seconds within ±300 seconds).
fn verify_webhook_hmac(
    secret: &str,
    headers: &HeaderMap,
    body: &[u8],
) -> Result<(), (StatusCode, Json<serde_json::Value>)> {
    let sig_hex = headers
        .get("x-signature")
        .and_then(|v| v.to_str().ok())
        .ok_or_else(|| json_err(StatusCode::UNAUTHORIZED, "missing X-Signature header"))?;

    let ts_str = headers
        .get("x-timestamp")
        .and_then(|v| v.to_str().ok())
        .ok_or_else(|| json_err(StatusCode::UNAUTHORIZED, "missing X-Timestamp header"))?;

    let ts_secs: i64 = ts_str
        .parse()
        .map_err(|_| json_err(StatusCode::UNAUTHORIZED, "invalid X-Timestamp header"))?;

    let now_secs = Utc::now().timestamp();
    if (now_secs - ts_secs).abs() > 300 {
        return Err(json_err(
            StatusCode::UNAUTHORIZED,
            "webhook timestamp outside 5-minute tolerance window",
        ));
    }

    let sig_bytes = hex::decode(sig_hex.trim())
        .map_err(|_| json_err(StatusCode::UNAUTHORIZED, "malformed hex in X-Signature"))?;

    let mut mac = HmacSha256::new_from_slice(secret.as_bytes())
        .map_err(|_| json_err(StatusCode::INTERNAL_SERVER_ERROR, "HMAC key init error"))?;
    mac.update(ts_str.as_bytes());
    mac.update(b".");
    mac.update(body);

    mac.verify_slice(&sig_bytes)
        .map_err(|_| json_err(StatusCode::UNAUTHORIZED, "invalid webhook HMAC signature"))
}

fn extract_actor(headers: &HeaderMap) -> (String, Option<StaffRole>, Option<String>) {
    let user = headers
        .get("x-actor-id")
        .and_then(|v| v.to_str().ok())
        .unwrap_or("")
        .to_string();
    let role = headers
        .get("x-actor-role")
        .and_then(|v| v.to_str().ok())
        .map(|r| match r.to_ascii_uppercase().as_str() {
            "FINANCE" | "BUSINESSOPERATIONSMANAGER" => StaffRole::Finance,
            "FACILITY_MANAGER" | "FACILITYMANAGER" => StaffRole::FacilityManager,
            "ADMIN" | "SYSTEMADMINISTRATOR" => StaffRole::Admin,
            "FACILITY_STAFF" | "FACILITYSTAFF" => StaffRole::FacilityStaff,
            _ => StaffRole::Customer,
        });
    let ip = headers
        .get("x-forwarded-for")
        .and_then(|v| v.to_str().ok())
        .map(|s| s.to_string());
    (user, role, ip)
}

async fn notify_sse(state: &AppState, req: &PaymentRequest) {
    let channels = state.sse_channels.lock().await;
    if let Some(tx) = channels.get(&req.id) {
        let _ = tx.send(PaymentSseEvent {
            payment_request_id: req.id,
            status: req.status,
            amount_vnd: req.amount_vnd,
            reference: req.reference.clone(),
            paid_at: req.paid_at,
        });
    }
}

async fn enqueue_booking_callback(
    db: &SqlitePool,
    req: &PaymentRequest,
    payment_id: Uuid,
    source: PaymentSource,
) {
    let event_id = Uuid::new_v4();
    let paid_at = req.paid_at.unwrap_or_else(Utc::now);
    let event = match req.purpose {
        PaymentPurpose::Deposit => BookingCallbackEvent::DepositPaid {
            event_id,
            payment_request_id: req.id,
            payment_id,
            subject_type: req.subject_type,
            subject_id: req.subject_id.clone(),
            reference: req.reference.clone(),
            amount_vnd: req.amount_vnd,
            source,
            paid_at,
        },
        PaymentPurpose::Rent | PaymentPurpose::Other => BookingCallbackEvent::RentPaid {
            event_id,
            payment_request_id: req.id,
            payment_id,
            subject_type: req.subject_type,
            subject_id: req.subject_id.clone(),
            reference: req.reference.clone(),
            amount_vnd: req.amount_vnd,
            source,
            paid_at,
        },
    };
    let event_type = match &event {
        BookingCallbackEvent::DepositPaid { .. } => "DepositPaid",
        BookingCallbackEvent::RentPaid { .. } => "RentPaid",
    };
    let payload_json = serde_json::to_string(&event).unwrap_or_default();
    let now = Utc::now().to_rfc3339();
    let _ = sqlx::query(
        "INSERT INTO callback_outbox (id, payment_request_id, event_type, payload_json, status, attempts, next_retry_at, created_at, delivered_at)
         VALUES (?, ?, ?, ?, 'PENDING', 0, ?, ?, NULL)",
    )
    .bind(event_id.to_string())
    .bind(req.id.to_string())
    .bind(event_type)
    .bind(payload_json)
    .bind(&now)
    .bind(&now)
    .execute(db)
    .await;
}

async fn expire_overdue_requests(state: &AppState) -> Result<u64, sqlx::Error> {
    let now = Utc::now().to_rfc3339();
    let res = sqlx::query(
        "UPDATE payment_request SET status = 'EXPIRED' WHERE status = 'PENDING' AND expires_at < ?",
    )
    .bind(now)
    .execute(&state.db)
    .await?;
    Ok(res.rows_affected())
}

async fn process_callback_outbox(state: &AppState) -> Result<(), sqlx::Error> {
    let Some(callback_url) = &state.config.callback_url else {
        return Ok(());
    };
    let now = Utc::now().to_rfc3339();
    let rows = sqlx::query(
        "SELECT id, payload_json, attempts FROM callback_outbox WHERE status = 'PENDING' AND next_retry_at <= ? LIMIT 20",
    )
    .bind(&now)
    .fetch_all(&state.db)
    .await?;

    let client = reqwest::Client::new();
    for r in rows {
        let id: String = r.get("id");
        let payload: String = r.get("payload_json");
        let attempts: i64 = r.get("attempts");
        let resp = client
            .post(callback_url)
            .header("Content-Type", "application/json")
            .header("X-Idempotency-Key", &id)
            .body(payload)
            .send()
            .await;

        if let Ok(ok_resp) = resp {
            if ok_resp.status().is_success() {
                let _ = sqlx::query(
                    "UPDATE callback_outbox SET status = 'DELIVERED', delivered_at = ? WHERE id = ?",
                )
                .bind(&now)
                .bind(&id)
                .execute(&state.db)
                .await;
                continue;
            }
        }

        let next_attempts = attempts + 1;
        let backoff_secs = (2_i64.pow(next_attempts.min(6) as u32)) * 10;
        let next_retry = (Utc::now() + chrono::Duration::seconds(backoff_secs)).to_rfc3339();
        let next_status = if next_attempts >= 10 { "FAILED" } else { "PENDING" };
        warn!(outbox_id = %id, attempts = next_attempts, "Booking callback delivery failed; scheduled retry");
        let _ = sqlx::query(
            "UPDATE callback_outbox SET status = ?, attempts = ?, next_retry_at = ? WHERE id = ?",
        )
        .bind(next_status)
        .bind(next_attempts)
        .bind(next_retry)
        .bind(&id)
        .execute(&state.db)
        .await;
    }
    Ok(())
}

async fn append_audit_log(
    db: &SqlitePool,
    entity_type: &str,
    entity_id: &str,
    action: &str,
    actor: &str,
    actor_role: Option<&str>,
    actor_ip: Option<&str>,
    before_state: Option<&str>,
    after_state: Option<&str>,
    reason: Option<&str>,
) {
    let _ = sqlx::query(
        "INSERT INTO payment_audit_log (id, entity_type, entity_id, action, actor, actor_role, actor_ip, before_state, after_state, reason, created_at)
         VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
    )
    .bind(Uuid::new_v4().to_string())
    .bind(entity_type)
    .bind(entity_id)
    .bind(action)
    .bind(actor)
    .bind(actor_role)
    .bind(actor_ip)
    .bind(before_state)
    .bind(after_state)
    .bind(reason)
    .bind(Utc::now().to_rfc3339())
    .execute(db)
    .await;
}

async fn load_payment_request(db: &SqlitePool, id: Uuid) -> Option<PaymentRequest> {
    let row = sqlx::query(
        "SELECT id, subject_type, subject_id, reference, code_token, purpose, amount_vnd, status, qr_payload, expires_at, created_at, paid_at
         FROM payment_request WHERE id = ? LIMIT 1",
    )
    .bind(id.to_string())
    .fetch_optional(db)
    .await
    .ok()??;
    Some(row_to_payment_request(&row))
}

async fn load_payment_record(db: &SqlitePool, id: Uuid) -> Option<PaymentRecord> {
    let r = sqlx::query(
        "SELECT id, payment_request_id, bank_transaction_id, provider_txn_ref, source, amount_vnd, recorded_by, approved_by, approval_pending, reversal_of_payment_id, reason, evidence, created_at
         FROM payment WHERE id = ? LIMIT 1",
    )
    .bind(id.to_string())
    .fetch_optional(db)
    .await
    .ok()??;

    Some(PaymentRecord {
        id: Uuid::parse_str(&r.get::<String, _>("id")).unwrap_or_default(),
        payment_request_id: Uuid::parse_str(&r.get::<String, _>("payment_request_id")).unwrap_or_default(),
        bank_transaction_id: r
            .get::<Option<String>, _>("bank_transaction_id")
            .and_then(|s| Uuid::parse_str(&s).ok()),
        provider_txn_ref: r.get("provider_txn_ref"),
        source: if r.get::<String, _>("source") == "AUTO" {
            PaymentSource::Auto
        } else {
            PaymentSource::Manual
        },
        amount_vnd: r.get("amount_vnd"),
        recorded_by: r.get("recorded_by"),
        approved_by: r.get("approved_by"),
        approval_pending: r.get::<i64, _>("approval_pending") != 0,
        reversal_of_payment_id: r
            .get::<Option<String>, _>("reversal_of_payment_id")
            .and_then(|s| Uuid::parse_str(&s).ok()),
        reason: r.get("reason"),
        evidence: r.get("evidence"),
        created_at: DateTime::parse_from_rfc3339(&r.get::<String, _>("created_at"))
            .map(|d| d.with_timezone(&Utc))
            .unwrap_or_else(|_| Utc::now()),
    })
}

fn row_to_payment_request(r: &sqlx::sqlite::SqliteRow) -> PaymentRequest {
    PaymentRequest {
        id: Uuid::parse_str(&r.get::<String, _>("id")).unwrap_or_default(),
        subject_type: match r.get::<String, _>("subject_type").as_str() {
            "CONTRACT" => SubjectType::Contract,
            _ => SubjectType::Reservation,
        },
        subject_id: r.get("subject_id"),
        reference: r.get("reference"),
        code_token: r.get("code_token"),
        purpose: match r.get::<String, _>("purpose").as_str() {
            "RENT" => PaymentPurpose::Rent,
            "OTHER" => PaymentPurpose::Other,
            _ => PaymentPurpose::Deposit,
        },
        amount_vnd: r.get("amount_vnd"),
        status: match r.get::<String, _>("status").as_str() {
            "PAID" => PaymentRequestStatus::Paid,
            "EXPIRED" => PaymentRequestStatus::Expired,
            "CANCELLED" => PaymentRequestStatus::Cancelled,
            "UNDER_REVIEW" => PaymentRequestStatus::UnderReview,
            "REFUNDED" => PaymentRequestStatus::Refunded,
            _ => PaymentRequestStatus::Pending,
        },
        qr_payload: r.get("qr_payload"),
        expires_at: DateTime::parse_from_rfc3339(&r.get::<String, _>("expires_at"))
            .map(|d| d.with_timezone(&Utc))
            .unwrap_or_else(|_| Utc::now()),
        created_at: DateTime::parse_from_rfc3339(&r.get::<String, _>("created_at"))
            .map(|d| d.with_timezone(&Utc))
            .unwrap_or_else(|_| Utc::now()),
        paid_at: r
            .get::<Option<String>, _>("paid_at")
            .and_then(|s| DateTime::parse_from_rfc3339(&s).ok())
            .map(|d| d.with_timezone(&Utc)),
    }
}

fn to_response(req: &PaymentRequest, cfg: &AppConfig) -> PaymentRequestResponse {
    PaymentRequestResponse {
        id: req.id,
        subject_type: req.subject_type,
        subject_id: req.subject_id.clone(),
        purpose: req.purpose,
        amount_vnd: req.amount_vnd,
        status: req.status,
        reference: req.reference.clone(),
        bank_name: cfg.bank_name.clone(),
        bank_bin: cfg.bank_bin.clone(),
        account_no: cfg.bank_account_no.clone(),
        account_name: cfg.bank_account_name.clone(),
        qr_payload: req.qr_payload.clone(),
        qr_png_url: format!("/payment-requests/{}/qr.png", req.id),
        qr_svg_url: format!("/payment-requests/{}/qr.svg", req.id),
        events_sse_url: format!("/payment-requests/{}/events", req.id),
        expires_at: req.expires_at,
        created_at: req.created_at,
        paid_at: req.paid_at,
    }
}

fn json_err(code: StatusCode, msg: &str) -> (StatusCode, Json<serde_json::Value>) {
    (
        code,
        Json(serde_json::json!({
            "error": msg,
        })),
    )
}

#[cfg(test)]
mod tests {
    use super::*;

    async fn test_state() -> AppState {
        let db = SqlitePool::connect("sqlite::memory:").await.unwrap();
        sqlx::query(include_str!("../../../migrations/0001_initial.sql"))
            .execute(&db)
            .await
            .unwrap();
        let config = AppConfig {
            demo_mode: true,
            bank_name: "TPBank".to_string(),
            bank_bin: "970423".to_string(),
            bank_account_no: "00000285636".to_string(),
            bank_account_name: "BUI DUC THIEN".to_string(),
            webhook_secret: "test-secret".to_string(),
            database_url: "sqlite::memory:".to_string(),
            bind_addr: "127.0.0.1:0".to_string(),
            callback_url: None,
            max_description_len: 23,
            dual_approval_threshold_vnd: 2_000_000,
            review_alert_minutes: 15,
        };
        AppState {
            db,
            config,
            mock_feed: MockBankFeed::new(),
            sse_channels: Arc::new(Mutex::new(HashMap::new())),
            reconcile_lock: Arc::new(Mutex::new(())),
        }
    }

    #[tokio::test]
    async fn two_simultaneous_webhooks_for_same_request_confirm_only_once() {
        let state = test_state().await;
        let (_, Json(created)) = create_payment_request(
            State(state.clone()),
            Json(CreatePaymentRequestInput {
                subject_type: SubjectType::Reservation,
                subject_id: "RS-261128-0001".to_string(),
                purpose: PaymentPurpose::Deposit,
                amount_vnd: 855_000,
                expires_at: Utc::now() + chrono::Duration::minutes(30),
                keyword: None,
            }),
        )
        .await
        .unwrap();

        let txn1 = IncomingTxn {
            provider: "SEPAY".to_string(),
            provider_txn_id: "FT-CONCURRENT-1".to_string(),
            amount_vnd: 855_000,
            is_credit: true,
            raw_description: created.reference.clone(),
            occurred_at: Utc::now(),
            raw_json: "{}".to_string(),
        };
        let txn2 = txn1.clone();

        let (r1, r2) = tokio::join!(
            reconcile_incoming_txn(&state, txn1),
            reconcile_incoming_txn(&state, txn2)
        );
        let d1 = r1.unwrap();
        let d2 = r2.unwrap();

        // Exactly one confirms PAID and the other is rejected as DuplicateTransaction
        let confirmed_count = [&d1, &d2]
            .iter()
            .filter(|d| matches!(d, ReconciliationDecision::AutoConfirmedPaid { .. }))
            .count();
        let dup_count = [&d1, &d2]
            .iter()
            .filter(|d| matches!(d, ReconciliationDecision::DuplicateTransaction { .. }))
            .count();

        assert_eq!(confirmed_count, 1);
        assert_eq!(dup_count, 1);

        let payment_count: i64 = sqlx::query_scalar("SELECT COUNT(*) FROM payment WHERE payment_request_id = ?")
            .bind(created.id.to_string())
            .fetch_one(&state.db)
            .await
            .unwrap();
        assert_eq!(payment_count, 1);
    }
}
