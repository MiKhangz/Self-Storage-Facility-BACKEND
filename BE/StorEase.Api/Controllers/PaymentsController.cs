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

    [HttpGet("vnpay-return")]
    [AllowAnonymous]
    public async Task<ActionResult<InvoiceDto>> VnPayReturn(CancellationToken ct)
    {
        var parameters = Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.Ordinal);
        var result = await invoices.ConfirmVnPayAsync(parameters, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpGet("vnpay-ipn")]
    [AllowAnonymous]
    public async Task<ActionResult<InvoiceDto>> VnPayIpn(CancellationToken ct)
    {
        var parameters = Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.Ordinal);
        var result = await invoices.ConfirmVnPayAsync(parameters, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost("vnpay-ipn")]
    [AllowAnonymous]
    public async Task<ActionResult<InvoiceDto>> VnPayIpnForm([FromForm] Dictionary<string, string> parameters, CancellationToken ct)
    {
        var result = await invoices.ConfirmVnPayAsync(parameters, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>Development only (404 elsewhere): the customer stands in for the gateway's success callback.</summary>
    [HttpPost("{paymentCode}/simulate-success")]
    [Authorize(Roles = Roles.StorageCustomer)]
    public async Task<ActionResult<InvoiceDto>> SimulateSuccess(string paymentCode, CancellationToken ct)
    {
        var result = await invoices.SimulateGatewaySuccessAsync(paymentCode, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
}
