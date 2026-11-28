//! `payments-core` — Domain types, bank feed traits, automatic reconciliation matching engine,
//! manual payment authorization & two-person approval rules, and state machine callback events.

use async_trait::async_trait;
use chrono::{DateTime, Duration, Utc};
use serde::{Deserialize, Serialize};
use thiserror::Error;
use uuid::Uuid;
use vietqr::{normalize_bank_description, normalize_code};

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "SCREAMING_SNAKE_CASE")]
pub enum SubjectType {
    Reservation,
    Contract,
}

impl SubjectType {
    pub fn as_str(&self) -> &'static str {
        match self {
            Self::Reservation => "RESERVATION",
            Self::Contract => "CONTRACT",
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "SCREAMING_SNAKE_CASE")]
pub enum PaymentPurpose {
    Deposit,
    Rent,
    Other,
}

impl PaymentPurpose {
    pub fn as_str(&self) -> &'static str {
        match self {
            Self::Deposit => "DEPOSIT",
            Self::Rent => "RENT",
            Self::Other => "OTHER",
        }
    }

    pub fn default_keyword(&self) -> &'static str {
        match self {
            Self::Deposit => "PAY",
            Self::Rent => "RENT",
            Self::Other => "PAY",
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "SCREAMING_SNAKE_CASE")]
pub enum PaymentRequestStatus {
    Pending,
    Paid,
    Expired,
    Cancelled,
    UnderReview,
    Refunded,
}

impl PaymentRequestStatus {
    pub fn as_str(&self) -> &'static str {
        match self {
            Self::Pending => "PENDING",
            Self::Paid => "PAID",
            Self::Expired => "EXPIRED",
            Self::Cancelled => "CANCELLED",
            Self::UnderReview => "UNDER_REVIEW",
            Self::Refunded => "REFUNDED",
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "SCREAMING_SNAKE_CASE")]
pub enum MatchStatus {
    Matched,
    Unmatched,
    Review,
    Duplicate,
}

impl MatchStatus {
    pub fn as_str(&self) -> &'static str {
        match self {
            Self::Matched => "MATCHED",
            Self::Unmatched => "UNMATCHED",
            Self::Review => "REVIEW",
            Self::Duplicate => "DUPLICATE",
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "SCREAMING_SNAKE_CASE")]
pub enum PaymentSource {
    Auto,
    Manual,
}

impl PaymentSource {
    pub fn as_str(&self) -> &'static str {
        match self {
            Self::Auto => "AUTO",
            Self::Manual => "MANUAL",
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "SCREAMING_SNAKE_CASE")]
pub enum StaffRole {
    Finance,
    FacilityManager,
    Admin,
    FacilityStaff,
    Customer,
}

impl StaffRole {
    /// Only `FINANCE`, `FACILITY_MANAGER`, and `ADMIN` have `payments.record_manual`.
    pub fn can_record_manual(&self) -> bool {
        matches!(self, Self::Finance | Self::FacilityManager | Self::Admin)
    }

    /// Only `FINANCE` and `ADMIN` have `payments.record_partial`.
    pub fn can_record_partial(&self) -> bool {
        matches!(self, Self::Finance | Self::Admin)
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct PaymentRequest {
    pub id: Uuid,
    pub subject_type: SubjectType,
    pub subject_id: String,
    pub reference: String,
    pub code_token: String,
    pub purpose: PaymentPurpose,
    pub amount_vnd: i64,
    pub status: PaymentRequestStatus,
    pub qr_payload: String,
    pub expires_at: DateTime<Utc>,
    pub created_at: DateTime<Utc>,
    pub paid_at: Option<DateTime<Utc>>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct BankTransaction {
    pub id: Uuid,
    pub provider: String,
    pub provider_txn_id: String,
    pub amount_vnd: i64,
    pub is_credit: bool,
    pub raw_description: String,
    pub normalized_description: String,
    pub occurred_at: DateTime<Utc>,
    pub received_at: DateTime<Utc>,
    pub raw_json: String,
    pub match_status: MatchStatus,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct PaymentRecord {
    pub id: Uuid,
    pub payment_request_id: Uuid,
    pub bank_transaction_id: Option<Uuid>,
    pub provider_txn_ref: Option<String>,
    pub source: PaymentSource,
    pub amount_vnd: i64,
    pub recorded_by: Option<String>,
    pub approved_by: Option<String>,
    pub approval_pending: bool,
    pub reversal_of_payment_id: Option<Uuid>,
    pub reason: Option<String>,
    pub evidence: Option<String>,
    pub created_at: DateTime<Utc>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct AuditLogEntry {
    pub id: Uuid,
    pub entity_type: String,
    pub entity_id: String,
    pub action: String,
    pub actor: String,
    pub actor_role: Option<String>,
    pub actor_ip: Option<String>,
    pub before_state: Option<String>,
    pub after_state: Option<String>,
    pub reason: Option<String>,
    pub created_at: DateTime<Utc>,
}

/// Event emitted to the main booking system (`StorEase.Api`) when a payment request is confirmed paid.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "event_type", rename_all = "PascalCase")]
pub enum BookingCallbackEvent {
    DepositPaid {
        event_id: Uuid,
        payment_request_id: Uuid,
        payment_id: Uuid,
        subject_type: SubjectType,
        subject_id: String,
        reference: String,
        amount_vnd: i64,
        source: PaymentSource,
        paid_at: DateTime<Utc>,
    },
    RentPaid {
        event_id: Uuid,
        payment_request_id: Uuid,
        payment_id: Uuid,
        subject_type: SubjectType,
        subject_id: String,
        reference: String,
        amount_vnd: i64,
        source: PaymentSource,
        paid_at: DateTime<Utc>,
    },
}

/// Incoming bank feed transaction before reconciliation.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct IncomingTxn {
    pub provider: String,
    pub provider_txn_id: String,
    pub amount_vnd: i64,
    pub is_credit: bool,
    pub raw_description: String,
    pub occurred_at: DateTime<Utc>,
    pub raw_json: String,
}

#[derive(Debug, Clone, Default, Serialize, Deserialize)]
pub struct Cursor(pub Option<String>);

#[derive(Debug, Error)]
pub enum PaymentError {
    #[error("forbidden: role '{0:?}' lacks required permission '{1}'")]
    Forbidden(StaffRole, &'static str),
    #[error("invalid amount: expected {expected} VND, got {actual} VND")]
    AmountMismatch { expected: i64, actual: i64 },
    #[error("amount must be > 0, got {0}")]
    NonPositiveAmount(i64),
    #[error("self-approval is forbidden: creator '{0}' cannot approve their own manual entry")]
    SelfApprovalForbidden(String),
    #[error("payment request '{0}' not found")]
    RequestNotFound(Uuid),
    #[error("payment '{0}' not found")]
    PaymentNotFound(Uuid),
    #[error("missing required field: {0}")]
    MissingField(&'static str),
    #[error("bank feed error: {0}")]
    FeedError(String),
}

#[async_trait]
pub trait BankFeed: Send + Sync {
    async fn fetch_new(&self, since: Cursor) -> Result<Vec<IncomingTxn>, PaymentError>;
}

/// Outcome of running the deterministic reconciliation matching engine on a single incoming transaction.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "outcome", rename_all = "SCREAMING_SNAKE_CASE")]
pub enum ReconciliationDecision {
    /// Repeat `provider_txn_id` — mark transaction `DUPLICATE` and ignore.
    DuplicateTransaction { existing_txn_id: Uuid },
    /// Outgoing debit transaction (`is_credit == false`) — ignored.
    IgnoredDebit,
    /// `provider_txn_id` matches an earlier `MANUAL` payment's bank reference: link it instead of double-counting.
    LinkedToManualPayment {
        payment_id: Uuid,
        payment_request_id: Uuid,
        amount_matches: bool,
    },
    /// Exact reference match + exact amount + request is `PENDING` and not expired (or within grace seconds).
    AutoConfirmedPaid {
        payment_request_id: Uuid,
        amount_vnd: i64,
    },
    /// Reference matched a `PENDING` request, but incoming amount != requested amount -> `UNDER_REVIEW`.
    UnderReviewAmountMismatch {
        payment_request_id: Uuid,
        expected_vnd: i64,
        received_vnd: i64,
        reason: String,
    },
    /// Reference matched an `EXPIRED` or `CANCELLED` request -> `UNDER_REVIEW` ("late payment").
    UnderReviewLatePayment {
        payment_request_id: Uuid,
        prior_status: PaymentRequestStatus,
        reason: String,
    },
    /// Reference matched a request that is already `PAID` -> `UNDER_REVIEW` ("possible duplicate payment").
    UnderReviewDuplicatePayment {
        payment_request_id: Uuid,
        reason: String,
    },
    /// No payment request reference matched -> `UNMATCHED` queue for staff.
    Unmatched,
}

/// Configurable grace period (in seconds) for the race where a transfer arrives a few seconds after `expires_at`.
pub const EXPIRY_RACE_GRACE_SECONDS: i64 = 30;

/// Pure matching engine function (unit-tested directly without DB I/O).
///
/// Rules:
/// 1. Dedupe by `provider_txn_id`.
/// 2. Only credits (`is_credit == true`, `amount_vnd > 0`) are considered.
/// 3. If a manual payment already recorded this `provider_txn_id`, link it rather than double-counting.
/// 4. Normalize description and match against active/recent requests by full reference or unique code token
///    (handling hyphen-mangled descriptions like `RSV-2026-00123` -> `RSV 2026 00123` -> collapsed `RSV202600123`).
/// 5. Exact reference + exact amount + `PENDING` and `occurred_at <= expires_at + grace` -> `AutoConfirmedPaid`.
/// 6. Reference matches `PENDING` but amount differs -> `UnderReviewAmountMismatch`.
/// 7. Reference matches `EXPIRED` or `CANCELLED` (or past grace) -> `UnderReviewLatePayment`.
/// 8. Reference matches `PAID` -> `UnderReviewDuplicatePayment`.
/// 9. Otherwise -> `Unmatched`.
pub fn evaluate_transaction(
    incoming: &IncomingTxn,
    existing_txn_with_same_provider_id: Option<Uuid>,
    manual_payment_with_same_ref: Option<&PaymentRecord>,
    candidate_requests: &[PaymentRequest],
    now: DateTime<Utc>,
) -> ReconciliationDecision {
    if let Some(existing_id) = existing_txn_with_same_provider_id {
        return ReconciliationDecision::DuplicateTransaction {
            existing_txn_id: existing_id,
        };
    }

    if !incoming.is_credit || incoming.amount_vnd <= 0 {
        return ReconciliationDecision::IgnoredDebit;
    }

    if let Some(manual) = manual_payment_with_same_ref {
        return ReconciliationDecision::LinkedToManualPayment {
            payment_id: manual.id,
            payment_request_id: manual.payment_request_id,
            amount_matches: manual.amount_vnd == incoming.amount_vnd,
        };
    }

    let matched_request = find_matching_request(&incoming.raw_description, candidate_requests);
    let Some(req) = matched_request else {
        return ReconciliationDecision::Unmatched;
    };

    match req.status {
        PaymentRequestStatus::Paid => ReconciliationDecision::UnderReviewDuplicatePayment {
            payment_request_id: req.id,
            reason: "possible duplicate payment (request already PAID)".to_string(),
        },
        PaymentRequestStatus::Expired | PaymentRequestStatus::Cancelled => {
            ReconciliationDecision::UnderReviewLatePayment {
                payment_request_id: req.id,
                prior_status: req.status,
                reason: format!("late payment on {} request", req.status.as_str()),
            }
        }
        PaymentRequestStatus::Pending | PaymentRequestStatus::UnderReview => {
            let effective_time = if incoming.occurred_at <= now {
                incoming.occurred_at
            } else {
                now
            };
            let grace_deadline = req.expires_at + Duration::seconds(EXPIRY_RACE_GRACE_SECONDS);
            if effective_time > grace_deadline {
                return ReconciliationDecision::UnderReviewLatePayment {
                    payment_request_id: req.id,
                    prior_status: PaymentRequestStatus::Expired,
                    reason: "late payment arrived after request expiry".to_string(),
                };
            }

            if incoming.amount_vnd != req.amount_vnd {
                let kind = if incoming.amount_vnd < req.amount_vnd {
                    "underpayment"
                } else {
                    "overpayment"
                };
                return ReconciliationDecision::UnderReviewAmountMismatch {
                    payment_request_id: req.id,
                    expected_vnd: req.amount_vnd,
                    received_vnd: incoming.amount_vnd,
                    reason: format!(
                        "{kind}: expected {} VND, received {} VND",
                        req.amount_vnd, incoming.amount_vnd
                    ),
                };
            }

            ReconciliationDecision::AutoConfirmedPaid {
                payment_request_id: req.id,
                amount_vnd: incoming.amount_vnd,
            }
        }
        PaymentRequestStatus::Refunded => ReconciliationDecision::UnderReviewLatePayment {
            payment_request_id: req.id,
            prior_status: PaymentRequestStatus::Refunded,
            reason: "payment arrived on already refunded request".to_string(),
        },
    }
}

/// Finds a matching `PaymentRequest` inside an arbitrary bank description.
/// Supports:
/// - Exact token sequence match (e.g. `"MBVCB 123 RSV202600123 PAY FT"`).
/// - Code token match (`"RSV202600123"` as a discrete whitespace-delimited token).
/// - Collapsed alphanumeric match when a customer or bank inserts hyphens/spaces inside the code
///   (e.g. `"Chuyển khoản RSV-2026-00123 PAY"` -> normalized `"CHUYEN KHOAN RSV 2026 00123 PAY"` -> collapsed `"CHUYENKHOANRSV202600123PAY"`).
pub fn find_matching_request<'a>(
    raw_description: &str,
    candidates: &'a [PaymentRequest],
) -> Option<&'a PaymentRequest> {
    let norm = normalize_bank_description(raw_description);
    if norm.is_empty() {
        return None;
    }
    let tokens: Vec<&str> = norm.split_whitespace().collect();
    let collapsed = normalize_code(raw_description);

    // 1. Prefer exact full-reference token sequence match or discrete code_token match
    for req in candidates {
        let code_tok = req.code_token.as_str();
        if code_tok.is_empty() {
            continue;
        }
        if tokens.contains(&code_tok) {
            return Some(req);
        }
    }

    // 2. Fallback: collapsed alphanumeric substring match for hyphen/space-split codes (minimum 6 chars to avoid false positives)
    for req in candidates {
        if req.code_token.len() >= 6 && collapsed.contains(&req.code_token) {
            return Some(req);
        }
    }

    None
}

/// Request payload for manual payment recording.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ManualPaymentInput {
    pub payment_request_id: Uuid,
    pub amount_vnd: i64,
    pub bank_reference_code: String,
    pub transfer_time: DateTime<Utc>,
    pub reason: String,
    pub evidence: String,
    pub actor_id: String,
    pub actor_role: StaffRole,
    pub actor_ip: Option<String>,
}

/// Validates a manual payment entry against role permissions, required fields, amount equality,
/// and dual-approval threshold. Returns `(needs_second_approver)`.
pub fn validate_manual_entry(
    input: &ManualPaymentInput,
    request_amount_vnd: i64,
    dual_approval_threshold_vnd: i64,
) -> Result<bool, PaymentError> {
    if !input.actor_role.can_record_manual() {
        return Err(PaymentError::Forbidden(
            input.actor_role,
            "payments.record_manual",
        ));
    }
    if input.amount_vnd <= 0 {
        return Err(PaymentError::NonPositiveAmount(input.amount_vnd));
    }
    if input.bank_reference_code.trim().is_empty() {
        return Err(PaymentError::MissingField("bank_reference_code"));
    }
    if input.reason.trim().is_empty() {
        return Err(PaymentError::MissingField("reason"));
    }
    if input.evidence.trim().is_empty() {
        return Err(PaymentError::MissingField("evidence"));
    }

    if input.amount_vnd != request_amount_vnd && !input.actor_role.can_record_partial() {
        return Err(PaymentError::AmountMismatch {
            expected: request_amount_vnd,
            actual: input.amount_vnd,
        });
    }

    let needs_second_approver = input.amount_vnd > dual_approval_threshold_vnd;
    Ok(needs_second_approver)
}

/// Validates second-person approval on a high-value manual payment entry.
pub fn validate_manual_approval(
    payment: &PaymentRecord,
    approver_id: &str,
    approver_role: StaffRole,
) -> Result<(), PaymentError> {
    if !approver_role.can_record_manual() {
        return Err(PaymentError::Forbidden(
            approver_role,
            "payments.approve_manual",
        ));
    }
    if let Some(creator) = &payment.recorded_by {
        if creator.eq_ignore_ascii_case(approver_id.trim()) {
            return Err(PaymentError::SelfApprovalForbidden(creator.clone()));
        }
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    fn sample_request(status: PaymentRequestStatus, amount: i64, expires_offset_secs: i64) -> PaymentRequest {
        let now = Utc::now();
        PaymentRequest {
            id: Uuid::new_v4(),
            subject_type: SubjectType::Reservation,
            subject_id: "RSV-2026-00123".to_string(),
            reference: "RSV202600123 PAY".to_string(),
            code_token: "RSV202600123".to_string(),
            purpose: PaymentPurpose::Deposit,
            amount_vnd: amount,
            status,
            qr_payload: "000201...".to_string(),
            expires_at: now + Duration::seconds(expires_offset_secs),
            created_at: now - Duration::minutes(10),
            paid_at: None,
        }
    }

    fn incoming_txn(amount: i64, desc: &str) -> IncomingTxn {
        IncomingTxn {
            provider: "SEPAY".to_string(),
            provider_txn_id: "TXN-001".to_string(),
            amount_vnd: amount,
            is_credit: true,
            raw_description: desc.to_string(),
            occurred_at: Utc::now(),
            raw_json: "{}".to_string(),
        }
    }

    #[test]
    fn exact_match_auto_confirms_paid() {
        let req = sample_request(PaymentRequestStatus::Pending, 855_000, 600);
        let txn = incoming_txn(855_000, "RSV202600123 PAY");
        let decision = evaluate_transaction(&txn, None, None, &[req.clone()], Utc::now());
        assert_eq!(
            decision,
            ReconciliationDecision::AutoConfirmedPaid {
                payment_request_id: req.id,
                amount_vnd: 855_000,
            }
        );
    }

    #[test]
    fn matches_hyphen_and_diacritic_mangled_description_with_prefix_and_suffix() {
        let req = sample_request(PaymentRequestStatus::Pending, 855_000, 600);
        let txn = incoming_txn(
            855_000,
            "IBFT.TPBANK.998877.Chuyển khoản đặt cọc RSV-2026-00123 PAY.CT tu 00000285636 BUI DUC THIEN",
        );
        let decision = evaluate_transaction(&txn, None, None, &[req.clone()], Utc::now());
        assert_eq!(
            decision,
            ReconciliationDecision::AutoConfirmedPaid {
                payment_request_id: req.id,
                amount_vnd: 855_000,
            }
        );
    }

    #[test]
    fn wrong_amount_goes_to_under_review() {
        let req = sample_request(PaymentRequestStatus::Pending, 855_000, 600);
        let txn = incoming_txn(500_000, "RSV202600123 PAY");
        let decision = evaluate_transaction(&txn, None, None, &[req.clone()], Utc::now());
        assert!(matches!(
            decision,
            ReconciliationDecision::UnderReviewAmountMismatch {
                payment_request_id,
                expected_vnd: 855_000,
                received_vnd: 500_000,
                ..
            } if payment_request_id == req.id
        ));
    }

    #[test]
    fn wrong_reference_goes_to_unmatched() {
        let req = sample_request(PaymentRequestStatus::Pending, 855_000, 600);
        let txn = incoming_txn(855_000, "CHUYEN TIEN MUA HANG RANDOM");
        let decision = evaluate_transaction(&txn, None, None, &[req], Utc::now());
        assert_eq!(decision, ReconciliationDecision::Unmatched);
    }

    #[test]
    fn late_payment_on_expired_request_goes_to_under_review() {
        let req = sample_request(PaymentRequestStatus::Expired, 855_000, -3600);
        let txn = incoming_txn(855_000, "RSV202600123 PAY");
        let decision = evaluate_transaction(&txn, None, None, &[req.clone()], Utc::now());
        assert!(matches!(
            decision,
            ReconciliationDecision::UnderReviewLatePayment {
                payment_request_id,
                prior_status: PaymentRequestStatus::Expired,
                ..
            } if payment_request_id == req.id
        ));
    }

    #[test]
    fn duplicate_txn_id_is_rejected_as_duplicate() {
        let req = sample_request(PaymentRequestStatus::Pending, 855_000, 600);
        let txn = incoming_txn(855_000, "RSV202600123 PAY");
        let existing_id = Uuid::new_v4();
        let decision = evaluate_transaction(&txn, Some(existing_id), None, &[req], Utc::now());
        assert_eq!(
            decision,
            ReconciliationDecision::DuplicateTransaction {
                existing_txn_id: existing_id
            }
        );
    }

    #[test]
    fn second_payment_on_already_paid_request_flagged_as_duplicate_payment() {
        let req = sample_request(PaymentRequestStatus::Paid, 855_000, 600);
        let txn = incoming_txn(855_000, "RSV202600123 PAY");
        let decision = evaluate_transaction(&txn, None, None, &[req.clone()], Utc::now());
        assert!(matches!(
            decision,
            ReconciliationDecision::UnderReviewDuplicatePayment {
                payment_request_id,
                ..
            } if payment_request_id == req.id
        ));
    }

    #[test]
    fn manual_entry_blocks_unauthorized_roles_and_self_approval() {
        let req_id = Uuid::new_v4();
        let bad_input = ManualPaymentInput {
            payment_request_id: req_id,
            amount_vnd: 855_000,
            bank_reference_code: "FT261128001".to_string(),
            transfer_time: Utc::now(),
            reason: "Webhook delayed".to_string(),
            evidence: "receipt_001.png".to_string(),
            actor_id: "staff_1".to_string(),
            actor_role: StaffRole::FacilityStaff,
            actor_ip: Some("127.0.0.1".to_string()),
        };
        assert!(matches!(
            validate_manual_entry(&bad_input, 855_000, 2_000_000),
            Err(PaymentError::Forbidden(StaffRole::FacilityStaff, "payments.record_manual"))
        ));

        let mut manager_input = bad_input.clone();
        manager_input.actor_id = "manager_1".to_string();
        manager_input.actor_role = StaffRole::FacilityManager;
        manager_input.amount_vnd = 5_000_000;
        let needs_approval = validate_manual_entry(&manager_input, 5_000_000, 2_000_000).unwrap();
        assert!(needs_approval, "5M VND exceeds 2M VND threshold");

        let pending_payment = PaymentRecord {
            id: Uuid::new_v4(),
            payment_request_id: req_id,
            bank_transaction_id: None,
            provider_txn_ref: Some("FT261128001".to_string()),
            source: PaymentSource::Manual,
            amount_vnd: 5_000_000,
            recorded_by: Some("manager_1".to_string()),
            approved_by: None,
            approval_pending: true,
            reversal_of_payment_id: None,
            reason: Some("Webhook delayed".to_string()),
            evidence: Some("receipt_001.png".to_string()),
            created_at: Utc::now(),
        };

        // Self-approval blocked
        assert!(matches!(
            validate_manual_approval(&pending_payment, "manager_1", StaffRole::FacilityManager),
            Err(PaymentError::SelfApprovalForbidden(_))
        ));

        // Second authorized person succeeds
        assert!(validate_manual_approval(&pending_payment, "finance_2", StaffRole::Finance).is_ok());
    }
}
