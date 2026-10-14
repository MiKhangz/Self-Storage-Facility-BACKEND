using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.Billing;
using StorEase.Application.Features.Invoices;
using StorEase.Application.Features.Invoices.Dtos;
using StorEase.Domain.Enums;

namespace StorEase.Api.Controllers;

[Route("api/invoices")]
[Authorize]
public sealed class InvoicesController(IInvoiceService invoices, IReminderService reminders) : ApiControllerBase
{
    /// <summary>A customer sees their own invoices; staff those of their facilities; Business Operations and admins all.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<InvoiceDto>>> List(InvoiceStatus? status, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await invoices.ListAsync(status, page, pageSize, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<InvoiceDto>> Get(int id, CancellationToken ct)
    {
        var result = await invoices.GetAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>
    /// Starts an online payment: returns a Pending payment and the checkout URL. The invoice is credited only when
    /// the gateway confirms through <c>POST /api/payments/gateway-callback</c>.
    /// </summary>
    [HttpPost("{id:int}/payments/online")]
    [Authorize(Roles = Roles.StorageCustomer)]
    public async Task<ActionResult<PaymentIntentDto>> PayOnline(int id, PaymentRequest request, CancellationToken ct)
    {
        var result = await invoices.PayOnlineAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost("{id:int}/payments/manual")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<InvoiceDto>> PayManual(int id, PaymentRequest request, CancellationToken ct)
    {
        var result = await invoices.RecordManualPaymentAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>Adds a charge (positive) or credit (negative) line, e.g. a damage charge or a goodwill discount.</summary>
    [HttpPost("{id:int}/adjustments")]
    [Authorize(Roles = Roles.BusinessOperationsManager)]
    public async Task<ActionResult<InvoiceDto>> Adjust(int id, InvoiceAdjustmentRequest request, CancellationToken ct)
    {
        var result = await invoices.AdjustAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpGet("{id:int}/reminders")]
    [Authorize(Roles = Roles.Staff + "," + Roles.BusinessOperationsManager + "," + Roles.SystemAdministrator)]
    public async Task<ActionResult<IReadOnlyList<ReminderDto>>> Reminders(int id, CancellationToken ct)
    {
        var result = await reminders.ListAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>Sends a payment reminder now, outside the daily schedule.</summary>
    [HttpPost("{id:int}/reminders")]
    [Authorize(Roles = Roles.Staff + "," + Roles.BusinessOperationsManager)]
    public async Task<ActionResult<ReminderDto>> SendReminder(int id, CancellationToken ct)
    {
        var result = await reminders.SendNowAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
}
