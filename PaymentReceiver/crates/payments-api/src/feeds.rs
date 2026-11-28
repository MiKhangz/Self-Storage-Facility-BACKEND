//! Isolated bank feed adapters (`MockBankFeed`, `WebhookFeed` for SePay / Casso / payOS, and `CsvImportFeed`).
//! Do NOT scrape banking apps or websites and do NOT store bank login credentials.

use std::sync::{Arc, Mutex};

use async_trait::async_trait;
use chrono::{DateTime, Utc};
use payments_core::{BankFeed, Cursor, IncomingTxn, PaymentError};
use serde::{Deserialize, Serialize};
use uuid::Uuid;

/// In-memory mock bank feed for the academic demo ("Simulate incoming transfer").
#[derive(Debug, Clone, Default)]
pub struct MockBankFeed {
    queue: Arc<Mutex<Vec<IncomingTxn>>>,
}

impl MockBankFeed {
    pub fn new() -> Self {
        Self::default()
    }

    pub fn push_simulated_transfer(&self, amount_vnd: i64, description: String) -> IncomingTxn {
        let txn_id = format!("MOCK-{}", Uuid::new_v4().simple());
        let now = Utc::now();
        let raw_json = serde_json::json!({
            "provider": "MOCK_FEED",
            "id": txn_id,
            "amount_vnd": amount_vnd,
            "description": description,
            "occurred_at": now,
        })
        .to_string();

        let txn = IncomingTxn {
            provider: "MOCK".to_string(),
            provider_txn_id: txn_id,
            amount_vnd,
            is_credit: true,
            raw_description: description,
            occurred_at: now,
            raw_json,
        };
        if let Ok(mut guard) = self.queue.lock() {
            guard.push(txn.clone());
        }
        txn
    }
}

#[async_trait]
impl BankFeed for MockBankFeed {
    async fn fetch_new(&self, _since: Cursor) -> Result<Vec<IncomingTxn>, PaymentError> {
        let mut guard = self
            .queue
            .lock()
            .map_err(|_| PaymentError::FeedError("mock queue lock poisoned".to_string()))?;
        let drained = guard.drain(..).collect();
        Ok(drained)
    }
}

/// Unified webhook payload mapper accepting common Vietnamese transaction notification schemas
/// (SePay, Casso, payOS, or canonical StorEase webhook JSON).
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(untagged)]
pub enum WebhookPayload {
    /// Canonical StorEase / Mock webhook format
    Canonical {
        provider: Option<String>,
        provider_txn_id: String,
        amount_vnd: i64,
        #[serde(default = "default_true")]
        is_credit: bool,
        description: String,
        occurred_at: Option<DateTime<Utc>>,
    },
    /// SePay webhook format (`id`, `gateway`, `transferAmount`, `transferType`: `"in"`/`"out"`, `content`, `referenceCode`)
    SePay {
        id: i64,
        gateway: Option<String>,
        #[serde(rename = "transferAmount")]
        transfer_amount: i64,
        #[serde(rename = "transferType")]
        transfer_type: Option<String>,
        content: String,
        #[serde(rename = "referenceCode")]
        reference_code: Option<String>,
    },
    /// Casso webhook format (`data: [{ id, tid, amount, description, when }]`)
    Casso { data: Vec<CassoTxnItem> },
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct CassoTxnItem {
    pub id: Option<i64>,
    pub tid: String,
    pub amount: i64,
    pub description: String,
}

fn default_true() -> bool {
    true
}

impl WebhookPayload {
    /// Maps any supported Vietnamese webhook notification payload into one or more canonical `IncomingTxn` items.
    pub fn into_incoming_txns(self, raw_json: &str) -> Vec<IncomingTxn> {
        let now = Utc::now();
        match self {
            WebhookPayload::Canonical {
                provider,
                provider_txn_id,
                amount_vnd,
                is_credit,
                description,
                occurred_at,
            } => vec![IncomingTxn {
                provider: provider.unwrap_or_else(|| "WEBHOOK".to_string()),
                provider_txn_id,
                amount_vnd,
                is_credit,
                raw_description: description,
                occurred_at: occurred_at.unwrap_or(now),
                raw_json: raw_json.to_string(),
            }],
            WebhookPayload::SePay {
                id,
                gateway,
                transfer_amount,
                transfer_type,
                content,
                reference_code,
            } => {
                let is_credit = transfer_type
                    .as_deref()
                    .map(|t| !t.eq_ignore_ascii_case("out"))
                    .unwrap_or(true);
                let txn_id = reference_code
                    .filter(|s| !s.trim().is_empty())
                    .unwrap_or_else(|| format!("SEPAY-{id}"));
                vec![IncomingTxn {
                    provider: gateway.unwrap_or_else(|| "SEPAY".to_string()),
                    provider_txn_id: txn_id,
                    amount_vnd: transfer_amount,
                    is_credit,
                    raw_description: content,
                    occurred_at: now,
                    raw_json: raw_json.to_string(),
                }]
            }
            WebhookPayload::Casso { data } => data
                .into_iter()
                .map(|item| IncomingTxn {
                    provider: "CASSO".to_string(),
                    provider_txn_id: item.tid,
                    amount_vnd: item.amount.abs(),
                    is_credit: item.amount > 0,
                    raw_description: item.description,
                    occurred_at: now,
                    raw_json: raw_json.to_string(),
                })
                .collect(),
        }
    }
}

/// CSV statement import adapter (`provider_txn_id,amount_vnd,description,occurred_at`).
pub struct CsvImportFeed {
    csv_content: String,
}

impl CsvImportFeed {
    pub fn new(csv_content: String) -> Self {
        Self { csv_content }
    }
}

#[derive(Debug, Deserialize)]
struct CsvRow {
    provider_txn_id: String,
    amount_vnd: i64,
    description: String,
    occurred_at: Option<DateTime<Utc>>,
}

#[async_trait]
impl BankFeed for CsvImportFeed {
    async fn fetch_new(&self, _since: Cursor) -> Result<Vec<IncomingTxn>, PaymentError> {
        let mut rdr = csv::ReaderBuilder::new()
            .trim(csv::Trim::All)
            .from_reader(self.csv_content.as_bytes());
        let mut out = Vec::new();
        for result in rdr.deserialize::<CsvRow>() {
            let row = result.map_err(|e| PaymentError::FeedError(e.to_string()))?;
            let raw_json = serde_json::json!({
                "provider": "CSV_IMPORT",
                "provider_txn_id": row.provider_txn_id,
                "amount_vnd": row.amount_vnd,
                "description": row.description,
            })
            .to_string();
            out.push(IncomingTxn {
                provider: "CSV_IMPORT".to_string(),
                provider_txn_id: row.provider_txn_id,
                amount_vnd: row.amount_vnd.abs(),
                is_credit: row.amount_vnd > 0,
                raw_description: row.description,
                occurred_at: row.occurred_at.unwrap_or_else(Utc::now),
                raw_json,
            });
        }
        Ok(out)
    }
}
