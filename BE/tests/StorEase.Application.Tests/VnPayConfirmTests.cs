using System.Web;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Invoices;
using StorEase.Application.Features.Invoices.Dtos;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

/// <summary>T-02: VNPay's answer (IPN or return URL) settles the pending gateway payment once.</summary>
public class VnPayConfirmTests
{
    private static readonly PaymentGatewayOptions Gateway = new() { VnPayTmnCode = "TESTCODE", VnPayHashSecret = "SECRET" };

    private static async Task<(TestDb Db, PaymentIntentDto Intent)> Pending()
    {
        var t = Create();
        t.Db.Add(new Invoice
        {
            CustomerId = CustomerId, InvoiceNumber = "INV-VNP", IssueDate = new DateOnly(2026, 10, 1), DueDate = new DateOnly(2026, 10, 10),
            TotalAmount = 855_000m, Status = InvoiceStatus.Issued,
        });
        t.Db.SaveChanges();
        var invoiceId = t.Db.Invoices.Single().InvoiceId;
        var intent = await new InvoiceService(t.Uow, Customer, Gateway).PayOnlineAsync(invoiceId, new PaymentRequest(855_000m, null), default);
        return (t, intent.Value!);
    }

    /// <summary>What VNPay sends back: the request's fields plus its result codes, signed with the shared secret.</summary>
    private static Dictionary<string, string> Answer(PaymentIntentDto intent, string responseCode, string? amount = null)
    {
        var query = HttpUtility.ParseQueryString(new Uri(intent.CheckoutUrl).Query);
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in query.AllKeys.Where(k => k != "vnp_SecureHash"))
            fields[key!] = query[key]!;
        fields["vnp_ResponseCode"] = responseCode;
        fields["vnp_TransactionStatus"] = responseCode;
        fields["vnp_TransactionNo"] = "14000001";
        if (amount is not null)
            fields["vnp_Amount"] = amount;
        var signed = string.Join("&", fields.Select(f => $"{System.Net.WebUtility.UrlEncode(f.Key)}={System.Net.WebUtility.UrlEncode(f.Value)}"));
        return new Dictionary<string, string>(fields) { ["vnp_SecureHash"] = VnPay.Sign("SECRET", signed) };
    }

    [Fact]
    public async Task A_successful_answer_pays_the_invoice_and_a_retry_changes_nothing()
    {
        var (t, intent) = await Pending();
        using var _ = t;
        var anonymous = new InvoiceService(t.Uow, new FakeCurrentUser(0, ""), Gateway);

        intent.CheckoutUrl.Should().StartWith(Gateway.VnPayUrl);
        var first = await anonymous.ConfirmVnPayAsync(Answer(intent, "00"), default);
        var retry = await anonymous.ConfirmVnPayAsync(Answer(intent, "00"), default);

        first.Value!.Status.Should().Be(InvoiceStatus.Paid);
        retry.IsSuccess.Should().BeTrue();
        (await t.Db.Payments.SingleAsync()).ReferenceNo.Should().Be("14000001");
    }

    [Fact]
    public async Task A_bad_signature_a_wrong_amount_or_a_cancelled_payment_credits_nothing()
    {
        var (t, intent) = await Pending();
        using var _ = t;
        var anonymous = new InvoiceService(t.Uow, new FakeCurrentUser(0, ""), Gateway);
        var forged = new Dictionary<string, string>(Answer(intent, "00")) { ["vnp_SecureHash"] = new string('0', 128) };

        (await anonymous.ConfirmVnPayAsync(forged, default)).Error!.Type.Should().Be(ErrorType.Unauthorized);
        (await anonymous.ConfirmVnPayAsync(Answer(intent, "00", amount: "100"), default)).Error!.Type.Should().Be(ErrorType.Validation);
        var cancelled = await anonymous.ConfirmVnPayAsync(Answer(intent, "24"), default);

        cancelled.IsSuccess.Should().BeTrue();
        (await t.Db.Payments.SingleAsync()).Status.Should().Be(PaymentStatus.Failed);
        (await t.Db.Invoices.SingleAsync()).PaidAmount.Should().Be(0m);
    }
}
