//! `vietqr` — Pure Rust library (no I/O) for building, validating, parsing, and rendering
//! EMVCo / NAPAS 247 VietQR payment payloads (dynamic IBFT to account `QRIBFTTA`).

use std::io::Cursor;

use image::{ImageFormat, Luma};
use qrcode::{render::svg, EcLevel, QrCode};
use serde::{Deserialize, Serialize};
use thiserror::Error;

/// NAPAS GUID for VietQR (Tag 38 -> Subtag 00).
pub const NAPAS_GUID: &str = "A000000727";
/// Service code for fast interbank transfer to account number (Tag 38 -> Subtag 02).
pub const SERVICE_CODE_ACCOUNT: &str = "QRIBFTTA";
/// ISO 4217 numeric currency code for Vietnamese Dong (Tag 53).
pub const CURRENCY_VND: &str = "704";
/// ISO 3166-1 alpha-2 country code for Vietnam (Tag 58).
pub const COUNTRY_VN: &str = "VN";
/// Default maximum normalized reference/description length (banking apps commonly truncate around 23–25 chars).
pub const DEFAULT_MAX_DESCRIPTION_LEN: usize = 23;
/// TPBank NAPAS acquirer BIN.
pub const BIN_TPBANK: &str = "970423";

#[derive(Debug, Error, PartialEq, Eq)]
pub enum VietQrError {
    #[error("bank BIN must be 6 ASCII digits, got '{0}'")]
    InvalidBin(String),
    #[error("account number must be 1..=19 ASCII digits, got '{0}'")]
    InvalidAccountNumber(String),
    #[error("amount in VND must be strictly positive (> 0), got {0}")]
    InvalidAmount(i64),
    #[error("description cannot be empty after normalization")]
    EmptyDescription,
    #[error("description exceeds maximum length of {max} characters: '{actual}'")]
    DescriptionTooLong { max: usize, actual: String },
    #[error("malformed TLV structure at offset {offset}: {reason}")]
    MalformedTlv { offset: usize, reason: String },
    #[error("missing required EMVCo tag '{0}'")]
    MissingTag(&'static str),
    #[error("unexpected value for tag {tag}: expected '{expected}', got '{actual}'")]
    UnexpectedTagValue {
        tag: &'static str,
        expected: &'static str,
        actual: String,
    },
    #[error("CRC mismatch: expected {expected}, found {actual}")]
    CrcMismatch { expected: String, actual: String },
    #[error("QR rendering failed: {0}")]
    QrRenderError(String),
}

/// Parsed representation of a dynamic VietQR payload.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct ParsedQr {
    pub payload_format_indicator: String,
    pub point_of_initiation: String,
    pub guid: String,
    pub bank_bin: String,
    pub account_no: String,
    pub service_code: String,
    pub currency: String,
    pub amount_vnd: i64,
    pub country: String,
    pub description: String,
    pub crc: String,
}

/// Computes CRC-16/CCITT-FALSE (polynomial `0x1021`, initial value `0xFFFF`, no final XOR)
/// over the given ASCII bytes.
pub fn crc16_ccitt_false(data: &[u8]) -> u16 {
    let mut crc: u16 = 0xFFFF;
    for &byte in data {
        crc ^= (byte as u16) << 8;
        for _ in 0..8 {
            if (crc & 0x8000) != 0 {
                crc = (crc << 1) ^ 0x1021;
            } else {
                crc <<= 1;
            }
        }
    }
    crc
}

/// Formats a single EMVCo TLV field (`TT` + 2-digit decimal byte length + value).
fn tlv(tag: &str, value: &str) -> String {
    debug_assert_eq!(tag.len(), 2);
    debug_assert!(value.len() <= 99);
    format!("{tag}{:02}{value}", value.len())
}

/// Strips Vietnamese diacritics, hyphens, spaces, and non-alphanumeric characters from a code,
/// converting it to uppercase ASCII (e.g. `"RSV-2026-00123"` -> `"RSV202600123"`, `"Đơn #01"` -> `"DON01"`).
pub fn normalize_code(input: &str) -> String {
    input
        .chars()
        .filter_map(fold_vietnamese_ascii)
        .filter(|c| c.is_ascii_alphanumeric())
        .map(|c| c.to_ascii_uppercase())
        .collect()
}

/// Builds a normalized VietQR reference (`"<NORMALIZED_CODE> <KEYWORD>"`) capped at `max_len` characters
/// (default 23). If the combined string exceeds `max_len`, the code prefix is trimmed from the right
/// only if still unique, or an error is returned when `strict` is used.
pub fn build_reference(
    raw_code: &str,
    purpose_keyword: &str,
    max_len: usize,
) -> Result<String, VietQrError> {
    let code = normalize_code(raw_code);
    let kw = normalize_code(purpose_keyword);
    if code.is_empty() {
        return Err(VietQrError::EmptyDescription);
    }
    let combined = if kw.is_empty() {
        code
    } else {
        format!("{code} {kw}")
    };
    if combined.len() > max_len {
        return Err(VietQrError::DescriptionTooLong {
            max: max_len,
            actual: combined,
        });
    }
    Ok(combined)
}

/// Normalizes an incoming bank transfer description into uppercase ASCII alphanumeric tokens
/// separated by single spaces, stripping punctuation, hyphens, and Vietnamese diacritics.
pub fn normalize_bank_description(raw: &str) -> String {
    let mut out = String::with_capacity(raw.len());
    let mut last_was_space = true;
    for ch in raw.chars() {
        if let Some(folded) = fold_vietnamese_ascii(ch) {
            if folded.is_ascii_alphanumeric() {
                out.push(folded.to_ascii_uppercase());
                last_was_space = false;
            } else if !last_was_space {
                out.push(' ');
                last_was_space = true;
            }
        } else if !last_was_space {
            out.push(' ');
            last_was_space = true;
        }
    }
    out.trim().to_string()
}

fn fold_vietnamese_ascii(ch: char) -> Option<char> {
    match ch {
        'a' | 'à' | 'á' | 'ả' | 'ã' | 'ạ' | 'ă' | 'ằ' | 'ắ' | 'ẳ' | 'ẵ' | 'ặ' | 'â' | 'ầ'
        | 'ấ' | 'ẩ' | 'ẫ' | 'ậ' => Some('A'),
        'A' | 'À' | 'Á' | 'Ả' | 'Ã' | 'Ạ' | 'Ă' | 'Ằ' | 'Ắ' | 'Ẳ' | 'Ẵ' | 'Ặ' | 'Â' | 'Ầ'
        | 'Ấ' | 'Ẩ' | 'Ẫ' | 'Ậ' => Some('A'),
        'd' | 'đ' | 'D' | 'Đ' => Some('D'),
        'e' | 'è' | 'é' | 'ẻ' | 'ẽ' | 'ẹ' | 'ê' | 'ề' | 'ế' | 'ể' | 'ễ' | 'ệ' => {
            Some('E')
        }
        'E' | 'È' | 'É' | 'Ẻ' | 'Ẽ' | 'Ẹ' | 'Ê' | 'Ề' | 'Ế' | 'Ể' | 'Ễ' | 'Ệ' => {
            Some('E')
        }
        'i' | 'ì' | 'í' | 'ỉ' | 'ĩ' | 'ị' | 'I' | 'Ì' | 'Í' | 'Ỉ' | 'Ĩ' | 'Ị' => {
            Some('I')
        }
        'o' | 'ò' | 'ó' | 'ỏ' | 'õ' | 'ọ' | 'ô' | 'ồ' | 'ố' | 'ổ' | 'ỗ' | 'ộ' | 'ơ' | 'ờ'
        | 'ớ' | 'ở' | 'ỡ' | 'ợ' => Some('O'),
        'O' | 'Ò' | 'Ó' | 'Ỏ' | 'Õ' | 'Ọ' | 'Ô' | 'Ồ' | 'Ố' | 'Ổ' | 'Ỗ' | 'Ộ' | 'Ơ' | 'Ờ'
        | 'Ớ' | 'Ở' | 'Ỡ' | 'Ợ' => Some('O'),
        'u' | 'ù' | 'ú' | 'ủ' | 'ũ' | 'ụ' | 'ư' | 'ừ' | 'ứ' | 'ử' | 'ữ' | 'ự' => {
            Some('U')
        }
        'U' | 'Ù' | 'Ú' | 'Ủ' | 'Ũ' | 'Ụ' | 'Ư' | 'Ừ' | 'Ứ' | 'Ử' | 'Ữ' | 'Ự' => {
            Some('U')
        }
        'y' | 'ỳ' | 'ý' | 'ỷ' | 'ỹ' | 'ỵ' | 'Y' | 'Ỳ' | 'Ý' | 'Ỷ' | 'Ỹ' | 'Ỵ' => {
            Some('Y')
        }
        c if c.is_ascii() => Some(c),
        _ => None,
    }
}

/// Builds a dynamic EMVCo / NAPAS VietQR payload with a fixed integer VND amount and purpose description.
///
/// Validation rules:
/// - `bin`: 6 ASCII digits (e.g. `"970423"` for TPBank).
/// - `account_no`: 1..=19 ASCII digits (spaces are stripped before validation; e.g. `"00000285636"`).
/// - `amount`: strictly positive integer VND (`> 0`).
/// - `description`: normalized to uppercase ASCII alphanumeric + single spaces, non-empty, and `<= DEFAULT_MAX_DESCRIPTION_LEN` (23 chars).
pub fn build_payload(
    bin: &str,
    account_no: &str,
    amount: i64,
    description: &str,
) -> Result<String, VietQrError> {
    build_payload_with_max_len(bin, account_no, amount, description, DEFAULT_MAX_DESCRIPTION_LEN)
}

/// Builds a dynamic VietQR payload with a custom maximum description length.
pub fn build_payload_with_max_len(
    bin: &str,
    account_no: &str,
    amount: i64,
    description: &str,
    max_desc_len: usize,
) -> Result<String, VietQrError> {
    let clean_bin = bin.trim();
    if clean_bin.len() != 6 || !clean_bin.chars().all(|c| c.is_ascii_digit()) {
        return Err(VietQrError::InvalidBin(bin.to_string()));
    }

    let clean_acc: String = account_no.chars().filter(|c| !c.is_whitespace()).collect();
    if clean_acc.is_empty()
        || clean_acc.len() > 19
        || !clean_acc.chars().all(|c| c.is_ascii_digit())
    {
        return Err(VietQrError::InvalidAccountNumber(account_no.to_string()));
    }

    if amount <= 0 {
        return Err(VietQrError::InvalidAmount(amount));
    }

    let clean_desc = normalize_bank_description(description);
    if clean_desc.is_empty() {
        return Err(VietQrError::EmptyDescription);
    }
    if clean_desc.len() > max_desc_len {
        return Err(VietQrError::DescriptionTooLong {
            max: max_desc_len,
            actual: clean_desc,
        });
    }

    // Tag 38: Merchant Account Information (NAPAS VietQR)
    let beneficiary = format!("{}{}", tlv("00", clean_bin), tlv("01", &clean_acc));
    let merchant_info = format!(
        "{}{}{}",
        tlv("00", NAPAS_GUID),
        tlv("01", &beneficiary),
        tlv("02", SERVICE_CODE_ACCOUNT)
    );

    // Tag 62: Additional Data Field Template -> Subtag 08: Purpose of Transaction
    let additional_data = tlv("08", &clean_desc);
    let amount_str = amount.to_string();

    let mut payload = String::with_capacity(160);
    payload.push_str(&tlv("00", "01"));
    payload.push_str(&tlv("01", "12"));
    payload.push_str(&tlv("38", &merchant_info));
    payload.push_str(&tlv("53", CURRENCY_VND));
    payload.push_str(&tlv("54", &amount_str));
    payload.push_str(&tlv("58", COUNTRY_VN));
    payload.push_str(&tlv("62", &additional_data));
    payload.push_str("6304");

    let crc = crc16_ccitt_false(payload.as_bytes());
    payload.push_str(&format!("{crc:04X}"));
    Ok(payload)
}

/// Parses and validates an EMVCo / NAPAS VietQR payload string, verifying the CRC-16/CCITT-FALSE checksum.
pub fn parse_payload(payload: &str) -> Result<ParsedQr, VietQrError> {
    if payload.len() < 12 {
        return Err(VietQrError::MalformedTlv {
            offset: 0,
            reason: "payload too short".to_string(),
        });
    }

    let crc_split = payload.len() - 4;
    let prefix = &payload[..crc_split];
    if !prefix.ends_with("6304") {
        return Err(VietQrError::MissingTag("63"));
    }
    let expected_crc_str = &payload[crc_split..];
    let computed_crc = format!("{:04X}", crc16_ccitt_false(prefix.as_bytes()));
    if !expected_crc_str.eq_ignore_ascii_case(&computed_crc) {
        return Err(VietQrError::CrcMismatch {
            expected: computed_crc,
            actual: expected_crc_str.to_string(),
        });
    }

    let top_fields = parse_tlv_sequence( payload)?;

    let get_tag = |tag: &'static str| -> Result<String, VietQrError> {
        top_fields
            .iter()
            .find(|(t, _)| t == tag)
            .map(|(_, v)| v.clone())
            .ok_or(VietQrError::MissingTag(tag))
    };

    let pfi = get_tag("00")?;
    if pfi != "01" {
        return Err(VietQrError::UnexpectedTagValue {
            tag: "00",
            expected: "01",
            actual: pfi,
        });
    }

    let poi = get_tag("01")?;
    let merchant_raw = get_tag("38")?;
    let currency = get_tag("53")?;
    if currency != CURRENCY_VND {
        return Err(VietQrError::UnexpectedTagValue {
            tag: "53",
            expected: CURRENCY_VND,
            actual: currency,
        });
    }

    let amount_raw = get_tag("54")?;
    let amount_vnd: i64 = amount_raw
        .parse()
        .map_err(|_| VietQrError::InvalidAmount(0))?;
    if amount_vnd <= 0 {
        return Err(VietQrError::InvalidAmount(amount_vnd));
    }

    let country = get_tag("58")?;
    if country != COUNTRY_VN {
        return Err(VietQrError::UnexpectedTagValue {
            tag: "58",
            expected: COUNTRY_VN,
            actual: country,
        });
    }

    let add_raw = get_tag("62")?;
    let crc = get_tag("63")?;

    let merchant_fields = parse_tlv_sequence(&merchant_raw)?;
    let guid = merchant_fields
        .iter()
        .find(|(t, _)| t == "00")
        .map(|(_, v)| v.clone())
        .ok_or(VietQrError::MissingTag("38.00"))?;
    if guid != NAPAS_GUID {
        return Err(VietQrError::UnexpectedTagValue {
            tag: "38.00",
            expected: NAPAS_GUID,
            actual: guid,
        });
    }

    let beneficiary_raw = merchant_fields
        .iter()
        .find(|(t, _)| t == "01")
        .map(|(_, v)| v.clone())
        .ok_or(VietQrError::MissingTag("38.01"))?;
    let service_code = merchant_fields
        .iter()
        .find(|(t, _)| t == "02")
        .map(|(_, v)| v.clone())
        .ok_or(VietQrError::MissingTag("38.02"))?;

    let ben_fields = parse_tlv_sequence(&beneficiary_raw)?;
    let bank_bin = ben_fields
        .iter()
        .find(|(t, _)| t == "00")
        .map(|(_, v)| v.clone())
        .ok_or(VietQrError::MissingTag("38.01.00"))?;
    let account_no = ben_fields
        .iter()
        .find(|(t, _)| t == "01")
        .map(|(_, v)| v.clone())
        .ok_or(VietQrError::MissingTag("38.01.01"))?;

    let add_fields = parse_tlv_sequence(&add_raw)?;
    let description = add_fields
        .iter()
        .find(|(t, _)| t == "08")
        .map(|(_, v)| v.clone())
        .ok_or(VietQrError::MissingTag("62.08"))?;

    Ok(ParsedQr {
        payload_format_indicator: pfi,
        point_of_initiation: poi,
        guid,
        bank_bin,
        account_no,
        service_code,
        currency,
        amount_vnd,
        country,
        description,
        crc: crc.to_uppercase(),
    })
}

fn parse_tlv_sequence(input: &str) -> Result<Vec<(String, String)>, VietQrError> {
    let bytes = input.as_bytes();
    let mut idx = 0;
    let mut fields = Vec::new();
    while idx < bytes.len() {
        if idx + 4 > bytes.len() {
            return Err(VietQrError::MalformedTlv {
                offset: idx,
                reason: "truncated TLV header".to_string(),
            });
        }
        let tag = &input[idx..idx + 2];
        let len_str = &input[idx + 2..idx + 4];
        let len: usize = len_str.parse().map_err(|_| VietQrError::MalformedTlv {
            offset: idx,
            reason: format!("non-numeric length '{len_str}'"),
        })?;
        let val_start = idx + 4;
        let val_end = val_start + len;
        if val_end > bytes.len() {
            return Err(VietQrError::MalformedTlv {
                offset: idx,
                reason: format!("length {len} exceeds remaining input"),
            });
        }
        let value = &input[val_start..val_end];
        fields.push((tag.to_string(), value.to_string()));
        idx = val_end;
    }
    Ok(fields)
}

/// Renders a VietQR payload string to a standalone SVG string with Error Correction Level M and quiet zone enabled.
pub fn render_svg(payload: &str) -> Result<String, VietQrError> {
    let code = QrCode::with_error_correction_level(payload.as_bytes(), EcLevel::M)
        .map_err(|e| VietQrError::QrRenderError(e.to_string()))?;
    let svg_str = code
        .render::<svg::Color>()
        .min_dimensions(280, 280)
        .quiet_zone(true)
        .dark_color(svg::Color("#111827"))
        .light_color(svg::Color("#ffffff"))
        .build();
    Ok(svg_str)
}

/// Renders a VietQR payload string to a PNG byte vector at the requested target pixel dimensions
/// (with Error Correction Level M and quiet zone included).
pub fn render_png(payload: &str, size: u32) -> Result<Vec<u8>, VietQrError> {
    let target = size.clamp(128, 2048);
    let code = QrCode::with_error_correction_level(payload.as_bytes(), EcLevel::M)
        .map_err(|e| VietQrError::QrRenderError(e.to_string()))?;
    let img = code
        .render::<Luma<u8>>()
        .min_dimensions(target, target)
        .quiet_zone(true)
        .build();

    let mut buf = Cursor::new(Vec::new());
    img.write_to(&mut buf, ImageFormat::Png)
        .map_err(|e| VietQrError::QrRenderError(e.to_string()))?;
    Ok(buf.into_inner())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn crc16_standard_check_vector() {
        // Standard CRC-16/CCITT-FALSE check value for ASCII "123456789" is 0x29B1.
        assert_eq!(crc16_ccitt_false(b"123456789"), 0x29B1);
    }

    #[test]
    fn round_trip_tpbank_vietqr_payload() {
        let reference = build_reference("RS-261127-867759", "PAY", DEFAULT_MAX_DESCRIPTION_LEN).unwrap();
        assert_eq!(reference, "RS261127867759 PAY");

        let payload = build_payload(BIN_TPBANK, "0000 0285 636", 855_000, &reference).unwrap();
        let parsed = parse_payload(&payload).unwrap();

        assert_eq!(parsed.payload_format_indicator, "01");
        assert_eq!(parsed.point_of_initiation, "12");
        assert_eq!(parsed.guid, NAPAS_GUID);
        assert_eq!(parsed.bank_bin, "970423");
        assert_eq!(parsed.account_no, "00000285636");
        assert_eq!(parsed.service_code, "QRIBFTTA");
        assert_eq!(parsed.currency, "704");
        assert_eq!(parsed.amount_vnd, 855_000);
        assert_eq!(parsed.country, "VN");
        assert_eq!(parsed.description, "RS261127867759 PAY");
        assert_eq!(parsed.crc.len(), 4);
    }

    #[test]
    fn rejects_invalid_inputs() {
        assert_eq!(
            build_payload("97042", "00000285636", 100_000, "RSV1 PAY"),
            Err(VietQrError::InvalidBin("97042".to_string()))
        );
        assert_eq!(
            build_payload("970423", "0000028563A", 100_000, "RSV1 PAY"),
            Err(VietQrError::InvalidAccountNumber("0000028563A".to_string()))
        );
        assert_eq!(
            build_payload("970423", "00000285636", 0, "RSV1 PAY"),
            Err(VietQrError::InvalidAmount(0))
        );
        assert_eq!(
            build_payload("970423", "00000285636", -50_000, "RSV1 PAY"),
            Err(VietQrError::InvalidAmount(-50_000))
        );
        assert_eq!(
            build_payload("970423", "00000285636", 100_000, "   ---   "),
            Err(VietQrError::EmptyDescription)
        );
        assert!(matches!(
            build_payload(
                "970423",
                "00000285636",
                100_000,
                "THIS DESCRIPTION IS WAY TOO LONG FOR VIETQR"
            ),
            Err(VietQrError::DescriptionTooLong { .. })
        ));
    }

    #[test]
    fn detects_tampered_crc() {
        let mut payload = build_payload("970423", "00000285636", 300_000, "RSV202600123 PAY").unwrap();
        let last = if payload.ends_with('0') { '1' } else { '0' };
        payload.pop();
        payload.push(last);

        assert!(matches!(
            parse_payload(&payload),
            Err(VietQrError::CrcMismatch { .. })
        ));
    }

    #[test]
    fn normalizes_vietnamese_diacritics_and_hyphens() {
        assert_eq!(normalize_code("RSV-2026-00123"), "RSV202600123");
        assert_eq!(
            normalize_bank_description("MBVCB.12345.Chuyển tiền đơn RSV-2026-00123 PAY cảm ơn"),
            "MBVCB 12345 CHUYEN TIEN DON RSV 2026 00123 PAY CAM ON"
        );
    }

    #[test]
    fn renders_svg_and_png_with_valid_headers() {
        let payload = build_payload("970423", "00000285636", 500_000, "RSV202600123 PAY").unwrap();
        let svg = render_svg(&payload).unwrap();
        assert!(svg.contains("<svg"));

        let png = render_png(&payload, 320).unwrap();
        // PNG magic header: 89 50 4E 47 0D 0A 1A 0A
        assert_eq!(&png[..8], &[0x89, b'P', b'N', b'G', 0x0D, 0x0A, 0x1A, 0x0A]);
    }
}
