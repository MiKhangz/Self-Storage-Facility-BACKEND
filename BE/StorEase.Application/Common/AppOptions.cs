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
    /// <summary>Shared secret for callback signatures. Empty means every callback is rejected.</summary>
    public string Secret { get; init; } = string.Empty;

    /// <summary>Lets a signed-in customer simulate the gateway's success callback. Development only.</summary>
    public bool AllowSimulation { get; init; }

    /// <summary>Shown to the customer as the place to pay; the demo checkout page.</summary>
    public string CheckoutBaseUrl { get; init; } = "http://localhost:5173/checkout";

    /// <summary>VNPay merchant code (vnp_TmnCode). With <see cref="VnPayHashSecret"/> it switches checkout to VNPay.</summary>
    public string VnPayTmnCode { get; init; } = string.Empty;

    /// <summary>VNPay secret for the HMAC-SHA512 signature. Keep it in user secrets, never in appsettings.</summary>
    public string VnPayHashSecret { get; init; } = string.Empty;

    public string VnPayUrl { get; init; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";

    /// <summary>Where VNPay sends the customer back: the frontend page that shows the result.</summary>
    public string VnPayReturnUrl { get; init; } = "http://localhost:5173/account/payments/vnpay-return";

    public bool UsesVnPay => VnPayTmnCode.Length > 0 && VnPayHashSecret.Length > 0;
}
