using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using StorEase.Application.Common;

namespace StorEase.Application.Features.Invoices;

/// <summary>
/// VNPay payment URL (API 2.1.0) and signature check. The signature is HMAC-SHA512 with the merchant's hash secret
/// over the vnp_* parameters sorted by name, each URL-encoded, joined as a query string.
/// </summary>
public static class VnPay
{
    private const string HashField = "vnp_SecureHash";

    public static string PaymentUrl(PaymentGatewayOptions options, string paymentCode, decimal amount, string clientIp, DateTime utcNow)
    {
        var vietnamNow = utcNow.AddHours(7);
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = options.VnPayTmnCode,
            // VNPay counts in hundredths of a dong.
            ["vnp_Amount"] = ((long)decimal.Round(amount * 100m)).ToString(CultureInfo.InvariantCulture),
            ["vnp_CurrCode"] = "VND",
            ["vnp_TxnRef"] = paymentCode,
            ["vnp_OrderInfo"] = $"Thanh toan {paymentCode}",
            ["vnp_OrderType"] = "other",
            ["vnp_Locale"] = "vn",
            ["vnp_ReturnUrl"] = options.VnPayReturnUrl,
            ["vnp_IpAddr"] = clientIp,
            ["vnp_CreateDate"] = vietnamNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            ["vnp_ExpireDate"] = vietnamNow.AddMinutes(15).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
        };
        var query = Query(fields);
        return $"{options.VnPayUrl}?{query}&{HashField}={Sign(options.VnPayHashSecret, query)}";
    }

    /// <summary>True when the vnp_* parameters VNPay sent (return URL or IPN) carry a valid signature.</summary>
    public static bool SignatureMatches(string hashSecret, IReadOnlyDictionary<string, string> parameters)
    {
        if (hashSecret.Length == 0 || !parameters.TryGetValue(HashField, out var given))
            return false;
        var signed = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in parameters)
            if (key.StartsWith("vnp_", StringComparison.Ordinal) && key is not (HashField or "vnp_SecureHashType"))
                signed[key] = value;
        var expected = Sign(hashSecret, Query(signed));
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(given.ToLowerInvariant()));
    }

    public static string Sign(string hashSecret, string query) =>
        Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(hashSecret), Encoding.UTF8.GetBytes(query))).ToLowerInvariant();

    private static string Query(SortedDictionary<string, string> fields) =>
        string.Join("&", fields.Where(f => f.Value.Length > 0).Select(f => $"{WebUtility.UrlEncode(f.Key)}={WebUtility.UrlEncode(f.Value)}"));
}
