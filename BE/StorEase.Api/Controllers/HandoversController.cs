using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.Handovers;
using StorEase.Application.Features.Handovers.Dtos;
using StorEase.Domain.Enums;

namespace StorEase.Api.Controllers;

[Route("api/handovers")]
public class HandoversController(IHandoverService handovers) : ApiControllerBase
{
    [HttpPost]
    [Authorize(Roles = Roles.StorageCustomer)]
    public async Task<ActionResult<HandoverDto>> Create(CreateHandoverRequest request, CancellationToken ct)
    {
        var result = await handovers.RequestAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value.HandoverId }, result.Value)
            : Failure(result.Error);
    }

    /// <summary>Staff see every request (queue: <c>status=Approved</c>); a customer sees only their own.</summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<PagedResult<HandoverDto>>> List(
        HandoverStatus? status, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await handovers.ListAsync(status, page, pageSize, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpGet("{id:int}")]
    [Authorize]
    public async Task<ActionResult<HandoverDto>> Get(int id, CancellationToken ct)
    {
        var result = await handovers.GetAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost("{id:int}/approve")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<HandoverDto>> Approve(int id, ApproveHandoverRequest request, CancellationToken ct)
    {
        var result = await handovers.ApproveAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost("{id:int}/inspection")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<HandoverDto>> Inspect(int id, CreateHandoverInspectionRequest request, CancellationToken ct)
    {
        var result = await handovers.InspectAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost("{id:int}/settle-deposit")]
    [Authorize(Roles = Roles.BusinessOperationsManager)]
    public async Task<ActionResult<DepositSettlementDto>> SettleDeposit(int id, CancellationToken ct)
    {
        var result = await handovers.SettleAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
}
