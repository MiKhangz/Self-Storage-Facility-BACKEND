using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using StorEase.Application.Common;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Invoices;

/// <summary>Builds and verifies the VNPay query-string protocol without putting credentials in code.</summary>
public static class VnPayGateway
{
    public static string BuildPaymentUrl(
        PaymentGatewayOptions options, string paymentCode, decimal amount, string? clientIp)
    {
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = options.VnPayTmnCode,
            ["vnp_Amount"] = decimal.Truncate(amount * 100m).ToString("0", CultureInfo.InvariantCulture),
            ["vnp_CurrCode"] = "VND",
            ["vnp_TxnRef"] = paymentCode,
            ["vnp_OrderInfo"] = $"StorEase payment {paymentCode}",
            ["vnp_OrderType"] = "other",
            ["vnp_Locale"] = "vn",
            ["vnp_ReturnUrl"] = options.VnPayReturnUrl,
            ["vnp_IpAddr"] = string.IsNullOrWhiteSpace(clientIp) ? options.VnPayIpAddress : clientIp,
            ["vnp_CreateDate"] = DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
        };

        var query = CanonicalQuery(values);
        var secureHash = Hash(options.VnPayHashSecret, query);
        return $"{options.VnPayPaymentUrl}?{query}&vnp_SecureHashType=HmacSHA512&vnp_SecureHash={secureHash}";
    }

    public static bool TryReadCallback(
        PaymentGatewayOptions options,
        IReadOnlyDictionary<string, string> parameters,
        out string paymentCode,
        out PaymentStatus status,
        out decimal amount,
        out string? referenceNo)
    {
        paymentCode = string.Empty;
        status = PaymentStatus.Failed;
        amount = 0;
        referenceNo = null;
        if (!options.IsVnPayConfigured
            || !parameters.TryGetValue("vnp_SecureHash", out var suppliedHash)
            || string.IsNullOrWhiteSpace(suppliedHash))
            return false;

        var signed = parameters
            .Where(p => p.Key.StartsWith("vnp_", StringComparison.Ordinal)
                        && !p.Key.Equals("vnp_SecureHash", StringComparison.Ordinal)
                        && !p.Key.Equals("vnp_SecureHashType", StringComparison.Ordinal))
            .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var expectedHash = Hash(options.VnPayHashSecret, CanonicalQuery(signed));
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(expectedHash.ToUpperInvariant()),
                Encoding.ASCII.GetBytes(suppliedHash.ToUpperInvariant())))
            return false;

        if (!parameters.TryGetValue("vnp_TxnRef", out var rawPaymentCode)
            || !parameters.TryGetValue("vnp_Amount", out var rawAmount)
            || !decimal.TryParse(rawAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out var minorAmount))
            return false;

        paymentCode = rawPaymentCode;
        amount = decimal.Round(minorAmount / 100m, 2, MidpointRounding.AwayFromZero);
        if (parameters.TryGetValue("vnp_TransactionNo", out var transactionNo))
            referenceNo = transactionNo;
        parameters.TryGetValue("vnp_ResponseCode", out var responseCode);
        parameters.TryGetValue("vnp_TransactionStatus", out var transactionStatus);
        status = responseCode == "00" && transactionStatus == "00"
            ? PaymentStatus.Succeeded
            : PaymentStatus.Failed;
        return true;
    }

    public static string ComputeCallbackSignature(PaymentGatewayOptions options, IReadOnlyDictionary<string, string> parameters)
    {
        var signed = parameters
            .Where(p => p.Key.StartsWith("vnp_", StringComparison.Ordinal)
                        && !p.Key.Equals("vnp_SecureHash", StringComparison.Ordinal)
                        && !p.Key.Equals("vnp_SecureHashType", StringComparison.Ordinal))
            .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        return Hash(options.VnPayHashSecret, CanonicalQuery(signed));
    }

    private static string CanonicalQuery(IReadOnlyDictionary<string, string> values) =>
        string.Join("&", values.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value ?? string.Empty)}"));

    private static string Hash(string secret, string data) =>
        Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(data)))
            .ToUpperInvariant();
}
