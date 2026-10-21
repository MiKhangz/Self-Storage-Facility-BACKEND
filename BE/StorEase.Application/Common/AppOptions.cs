namespace StorEase.Application.Common;

/// <summary>Bound from the <c>App</c> config section.</summary>
public sealed class AppOptions
{
    /// <summary>Where links in emails point (verify email, reset password).</summary>
    public string FrontendBaseUrl { get; init; } = "http://localhost:5173";

    /// <summary>New accounts start Active with no verification email. Development only: there is no real email yet.</summary>
    public bool AutoVerifyEmail { get; init; }
}

/// <summary>
/// Bound from the <c>PaymentGateway</c> config section. There is no real provider yet: the "gateway" confirms a
/// payment by calling back with an HMAC signature made with <see cref="Secret"/>.
/// </summary>
public sealed class PaymentGatewayOptions
{
    /// <summary>Set to <c>VNPay</c> after the sandbox merchant credentials are configured.</summary>
    public string Provider { get; init; } = "Mock";

    /// <summary>Shared secret for callback signatures. Empty means every callback is rejected.</summary>
    public string Secret { get; init; } = string.Empty;

    /// <summary>Lets a signed-in customer simulate the gateway's success callback. Development only.</summary>
    public bool AllowSimulation { get; init; }

    /// <summary>Shown to the customer as the place to pay; the demo checkout page.</summary>
    public string CheckoutBaseUrl { get; init; } = "http://localhost:5173/checkout";

    /// <summary>VNPay merchant code. Keep it in appsettings.Development.json or user secrets.</summary>
    public string VnPayTmnCode { get; init; } = string.Empty;

    /// <summary>VNPay HMAC-SHA512 secret. Never commit this value.</summary>
    public string VnPayHashSecret { get; init; } = string.Empty;

    public string VnPayPaymentUrl { get; init; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
    public string VnPayReturnUrl { get; init; } = "https://localhost:7001/api/payments/vnpay-return";
    public string VnPayIpAddress { get; init; } = "127.0.0.1";

    public bool IsVnPayConfigured => Provider.Equals("VNPay", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(VnPayTmnCode)
        && !string.IsNullOrWhiteSpace(VnPayHashSecret)
        && Uri.TryCreate(VnPayPaymentUrl, UriKind.Absolute, out _)
        && Uri.TryCreate(VnPayReturnUrl, UriKind.Absolute, out _);
}
