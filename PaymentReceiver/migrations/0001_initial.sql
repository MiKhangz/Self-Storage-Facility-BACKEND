-- 0001_initial.sql
-- StorEase PaymentReceiver schema (SQLite / Postgres compatible)
-- All monetary amounts are integer VND (BIGINT / INTEGER). All timestamps are UTC ISO-8601.

CREATE TABLE IF NOT EXISTS payment_request (
    id                  TEXT PRIMARY KEY,
    subject_type        TEXT NOT NULL CHECK (subject_type IN ('RESERVATION', 'CONTRACT')),
    subject_id          TEXT NOT NULL,
    reference           TEXT NOT NULL,
    code_token          TEXT NOT NULL,
    purpose             TEXT NOT NULL CHECK (purpose IN ('DEPOSIT', 'RENT', 'OTHER')),
    amount_vnd          BIGINT NOT NULL CHECK (amount_vnd > 0),
    status              TEXT NOT NULL CHECK (status IN ('PENDING', 'PAID', 'EXPIRED', 'CANCELLED', 'UNDER_REVIEW', 'REFUNDED')),
    qr_payload          TEXT NOT NULL,
    expires_at          TEXT NOT NULL,
    created_at          TEXT NOT NULL,
    paid_at             TEXT
);

CREATE INDEX IF NOT EXISTS idx_payment_request_subject_pending
    ON payment_request (subject_type, subject_id, purpose, status);
CREATE INDEX IF NOT EXISTS idx_payment_request_reference
    ON payment_request (reference);
CREATE INDEX IF NOT EXISTS idx_payment_request_status_expires
    ON payment_request (status, expires_at);

CREATE TABLE IF NOT EXISTS bank_transaction (
    id                      TEXT PRIMARY KEY,
    provider                TEXT NOT NULL,
    provider_txn_id         TEXT NOT NULL UNIQUE,
    amount_vnd              BIGINT NOT NULL,
    is_credit               INTEGER NOT NULL DEFAULT 1,
    raw_description         TEXT NOT NULL,
    normalized_description  TEXT NOT NULL,
    occurred_at             TEXT NOT NULL,
    received_at             TEXT NOT NULL,
    raw_json                TEXT NOT NULL,
    match_status            TEXT NOT NULL CHECK (match_status IN ('MATCHED', 'UNMATCHED', 'REVIEW', 'DUPLICATE'))
);

CREATE INDEX IF NOT EXISTS idx_bank_transaction_match_status
    ON bank_transaction (match_status);

CREATE TABLE IF NOT EXISTS payment (
    id                      TEXT PRIMARY KEY,
    payment_request_id      TEXT NOT NULL REFERENCES payment_request(id),
    bank_transaction_id     TEXT REFERENCES bank_transaction(id),
    provider_txn_ref        TEXT,
    source                  TEXT NOT NULL CHECK (source IN ('AUTO', 'MANUAL')),
    amount_vnd              BIGINT NOT NULL,
    recorded_by             TEXT,
    approved_by             TEXT,
    approval_pending        INTEGER NOT NULL DEFAULT 0,
    reversal_of_payment_id  TEXT REFERENCES payment(id),
    reason                  TEXT,
    evidence                TEXT,
    created_at              TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_payment_request_id
    ON payment (payment_request_id);
CREATE INDEX IF NOT EXISTS idx_payment_provider_txn_ref
    ON payment (provider_txn_ref);

CREATE TABLE IF NOT EXISTS payment_audit_log (
    id              TEXT PRIMARY KEY,
    entity_type     TEXT NOT NULL,
    entity_id       TEXT NOT NULL,
    action          TEXT NOT NULL,
    actor           TEXT NOT NULL,
    actor_role      TEXT,
    actor_ip        TEXT,
    before_state    TEXT,
    after_state     TEXT,
    reason          TEXT,
    created_at      TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_audit_entity
    ON payment_audit_log (entity_type, entity_id, created_at);

-- Prevent UPDATE or DELETE on append-only payment_audit_log
CREATE TRIGGER IF NOT EXISTS trg_payment_audit_log_no_update
BEFORE UPDATE ON payment_audit_log
BEGIN
    SELECT RAISE(ABORT, 'payment_audit_log is append-only: UPDATE is forbidden');
END;

CREATE TRIGGER IF NOT EXISTS trg_payment_audit_log_no_delete
BEFORE DELETE ON payment_audit_log
BEGIN
    SELECT RAISE(ABORT, 'payment_audit_log is append-only: DELETE is forbidden');
END;

CREATE TABLE IF NOT EXISTS callback_outbox (
    id                  TEXT PRIMARY KEY,
    payment_request_id  TEXT NOT NULL REFERENCES payment_request(id),
    event_type          TEXT NOT NULL,
    payload_json        TEXT NOT NULL,
    status              TEXT NOT NULL CHECK (status IN ('PENDING', 'DELIVERED', 'FAILED')),
    attempts            INTEGER NOT NULL DEFAULT 0,
    next_retry_at       TEXT NOT NULL,
    created_at          TEXT NOT NULL,
    delivered_at        TEXT
);

CREATE INDEX IF NOT EXISTS idx_callback_outbox_pending
    ON callback_outbox (status, next_retry_at);
