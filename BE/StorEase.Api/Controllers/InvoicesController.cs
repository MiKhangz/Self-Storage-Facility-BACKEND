using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.Invoices;
using StorEase.Application.Features.Invoices.Dtos;
using StorEase.Domain.Enums;

namespace StorEase.Api.Controllers;

[Route("api/invoices")]
[Authorize]
public sealed class InvoicesController(IInvoiceService invoices) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Roles = Roles.StorageCustomer + "," + Roles.FacilityStaff + "," + Roles.FacilityManager + "," + Roles.BusinessOperationsManager + "," + Roles.SystemAdministrator)]
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

    [HttpPost("{id:int}/payments/online")]
    [Authorize(Roles = Roles.StorageCustomer)]
    public async Task<ActionResult<InvoiceDto>> PayOnline(int id, OnlinePaymentRequest request, CancellationToken ct)
    {
        var result = await invoices.PayOnlineAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost("{id:int}/payments/manual")]
    [Authorize(Roles = Roles.FacilityStaff + "," + Roles.FacilityManager)]
    public async Task<ActionResult<InvoiceDto>> PayManual(int id, ManualPaymentRequest request, CancellationToken ct)
    {
        var result = await invoices.RecordManualPaymentAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
}
