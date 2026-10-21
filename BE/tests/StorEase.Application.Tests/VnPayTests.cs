using System.Web;
using FluentAssertions;
using StorEase.Application.Common;
using StorEase.Application.Features.Invoices;

namespace StorEase.Application.Tests;

public class VnPayTests
{
    private static readonly PaymentGatewayOptions Options = new() { VnPayTmnCode = "TESTCODE", VnPayHashSecret = "SECRET" };

    private static Dictionary<string, string> ParamsOf(string url)
    {
        var query = HttpUtility.ParseQueryString(new Uri(url).Query);
        return query.AllKeys.ToDictionary(k => k!, k => query[k]!);
    }

    [Fact]
    public void A_payment_url_carries_the_amount_in_hundredths_and_a_signature_that_verifies()
    {
        var url = VnPay.PaymentUrl(Options, "PY261021000001", 855_000m, "127.0.0.1", new DateTime(2026, 10, 21, 3, 0, 0, DateTimeKind.Utc));
        var fields = ParamsOf(url);

        url.Should().StartWith(Options.VnPayUrl + "?");
        fields["vnp_Amount"].Should().Be("85500000");
        fields["vnp_TxnRef"].Should().Be("PY261021000001");
        fields["vnp_CreateDate"].Should().Be("20261021100000", "VNPay expects Vietnam time");
        VnPay.SignatureMatches("SECRET", fields).Should().BeTrue();
    }

    [Fact]
    public void A_changed_amount_or_a_wrong_secret_fails_the_signature()
    {
        var fields = ParamsOf(VnPay.PaymentUrl(Options, "PY1", 100m, "127.0.0.1", DateTime.UtcNow));
        var tampered = new Dictionary<string, string>(fields) { ["vnp_Amount"] = "1" };

        VnPay.SignatureMatches("SECRET", tampered).Should().BeFalse();
        VnPay.SignatureMatches("OTHER", fields).Should().BeFalse();
        VnPay.SignatureMatches("", fields).Should().BeFalse();
    }
}
