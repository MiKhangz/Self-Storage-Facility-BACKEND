//! Environment configuration and startup safety checks.
//! Never logs full account numbers (masks all but the last 3 digits).

use std::env;

#[derive(Debug, Clone)]
pub struct AppConfig {
    pub demo_mode: bool,
    pub bank_name: String,
    pub bank_bin: String,
    pub bank_account_no: String,
    pub bank_account_name: String,
    pub webhook_secret: String,
    pub database_url: String,
    pub bind_addr: String,
    pub callback_url: Option<String>,
    pub max_description_len: usize,
    pub dual_approval_threshold_vnd: i64,
    pub review_alert_minutes: i64,
}

impl AppConfig {
    pub fn from_env() -> Result<Self, String> {
        let demo_mode = env::var("DEMO_MODE")
            .unwrap_or_else(|_| "true".to_string())
            .parse::<bool>()
            .unwrap_or(true);

        // User-provided TPBank VietQR demo account defaults when DEMO_MODE=true
        let bank_name = env::var("BANK_NAME").unwrap_or_else(|_| "TPBank".to_string());
        let bank_bin = env::var("BANK_BIN").unwrap_or_else(|_| "970423".to_string());
        let bank_account_no = env::var("BANK_ACCOUNT_NO")
            .unwrap_or_else(|_| "00000285636".to_string())
            .chars()
            .filter(|c| !c.is_whitespace())
            .collect::<String>();
        let bank_account_name =
            env::var("BANK_ACCOUNT_NAME").unwrap_or_else(|_| "BUI DUC THIEN".to_string());
        let webhook_secret = env::var("WEBHOOK_SECRET")
            .unwrap_or_else(|_| "storease-demo-webhook-hmac-secret-2026".to_string());
        let database_url = env::var("DATABASE_URL")
            .unwrap_or_else(|_| "sqlite://payments_demo.db?mode=rwc".to_string());
        let bind_addr = env::var("BIND_ADDR").unwrap_or_else(|_| "0.0.0.0:4100".to_string());
        let callback_url = env::var("BOOKING_CALLBACK_URL").ok();
        let max_description_len = env::var("MAX_DESCRIPTION_LEN")
            .ok()
            .and_then(|v| v.parse().ok())
            .unwrap_or(23);
        let dual_approval_threshold_vnd = env::var("DUAL_APPROVAL_THRESHOLD_VND")
            .ok()
            .and_then(|v| v.parse().ok())
            .unwrap_or(5_000_000);
        let review_alert_minutes = env::var("REVIEW_ALERT_MINUTES")
            .ok()
            .and_then(|v| v.parse().ok())
            .unwrap_or(15);

        let cfg = Self {
            demo_mode,
            bank_name,
            bank_bin,
            bank_account_no,
            bank_account_name,
            webhook_secret,
            database_url,
            bind_addr,
            callback_url,
            max_description_len,
            dual_approval_threshold_vnd,
            review_alert_minutes,
        };
        cfg.validate_startup()?;
        Ok(cfg)
    }

    /// Startup check:
    /// - Refuses to run if `DEMO_MODE=false` and account details or webhook secret look like placeholders.
    /// - Validates BIN (6 digits) and account number (digits only).
    pub fn validate_startup(&self) -> Result<(), String> {
        if self.bank_bin.len() != 6 || !self.bank_bin.chars().all(|c| c.is_ascii_digit()) {
            return Err("BANK_BIN must be 6 ASCII digits".to_string());
        }
        if self.bank_account_no.is_empty()
            || !self.bank_account_no.chars().all(|c| c.is_ascii_digit())
        {
            return Err("BANK_ACCOUNT_NO must contain only digits".to_string());
        }

        let looks_like_placeholder = self.bank_account_no.contains("123456")
            || self.bank_account_name.eq_ignore_ascii_case("PLACEHOLDER")
            || self.webhook_secret.contains("demo-webhook");

        if !self.demo_mode && looks_like_placeholder {
            return Err(
                "Startup refused: DEMO_MODE is false, but BANK_ACCOUNT_NO / WEBHOOK_SECRET look like demo or placeholder values."
                    .to_string(),
            );
        }
        Ok(())
    }

    /// Masks the account number for logs (e.g. `"00000285636"` -> `"••••••••636"`).
    pub fn masked_account_no(&self) -> String {
        let len = self.bank_account_no.len();
        if len <= 3 {
            return "***".to_string();
        }
        let suffix = &self.bank_account_no[len - 3..];
        format!("••••••••{suffix}")
    }
}
