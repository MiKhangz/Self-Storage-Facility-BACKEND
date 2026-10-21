using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.Invoices;
using StorEase.Application.Features.Invoices.Dtos;
using StorEase.Domain.Enums;

namespace StorEase.Api.Controllers;

[Route("api/payments")]
public sealed class PaymentsController(IInvoiceService invoices) : ApiControllerBase
{
    /// <summary>Payment history: a customer sees their own, staff their facilities', Business Operations and admins all.</summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<PagedResult<PaymentHistoryDto>>> List(
        int? invoiceId, PaymentStatus? status, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await invoices.ListPaymentsAsync(invoiceId, status, page, pageSize, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost("{id:int}/refund")]
    [Authorize(Roles = Roles.BusinessOperationsManager)]
    public async Task<ActionResult<InvoiceDto>> Refund(int id, RefundRequest request, CancellationToken ct)
    {
        var result = await invoices.RefundPaymentAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>Called by the payment gateway, authenticated by the HMAC signature in the body (no bearer token).</summary>
    [HttpPost("gateway-callback")]
    [AllowAnonymous]
    public async Task<ActionResult<InvoiceDto>> GatewayCallback(GatewayCallbackRequest request, CancellationToken ct)
    {
        var result = await invoices.ConfirmGatewayAsync(request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>
    /// VNPay's server-to-server IPN. Answers in VNPay's format with 200 every time; RspCode 00 means the result is
    /// recorded (or was already), anything else makes VNPay retry or report the error.
    /// </summary>
    [HttpGet("vnpay-ipn")]
    [AllowAnonymous]
    public async Task<IActionResult> VnPayIpn(CancellationToken ct)
    {
        var result = await invoices.ConfirmVnPayAsync(VnPayParameters(), ct);
        var (code, message) = result.IsSuccess ? ("00", "Confirm Success") : result.Error!.Type switch
        {
            ErrorType.Unauthorized => ("97", "Invalid signature"),
            ErrorType.NotFound => ("01", "Order not found"),
            ErrorType.Validation => ("04", "Invalid amount"),
            ErrorType.Conflict => ("02", "Order already confirmed"),
            _ => ("99", "Unknown error"),
        };
        return Ok(new { RspCode = code, Message = message });
    }

    /// <summary>
    /// The frontend forwards VNPay's return-URL parameters here. Same check as the IPN, so a local setup that VNPay's
    /// IPN cannot reach still records the payment; returns the invoice.
    /// </summary>
    [HttpGet("vnpay-return")]
    [AllowAnonymous]
    public async Task<ActionResult<InvoiceDto>> VnPayReturn(CancellationToken ct)
    {
        var result = await invoices.ConfirmVnPayAsync(VnPayParameters(), ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    private Dictionary<string, string> VnPayParameters() =>
        Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());

    /// <summary>Development only (404 elsewhere): the customer stands in for the gateway's success callback.</summary>
    [HttpPost("{paymentCode}/simulate-success")]
    [Authorize(Roles = Roles.StorageCustomer)]
    public async Task<ActionResult<InvoiceDto>> SimulateSuccess(string paymentCode, CancellationToken ct)
    {
        var result = await invoices.SimulateGatewaySuccessAsync(paymentCode, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
}
