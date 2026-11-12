using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.Returns;
using StorEase.Application.Features.Returns.Dtos;
using StorEase.Domain.Enums;

namespace StorEase.Api.Controllers;

[Route("api/returns")]
public class ReturnsController(IReturnService unitReturns) : ApiControllerBase
{
    [HttpPost]
    [Authorize(Roles = Roles.StorageCustomer)]
    public async Task<ActionResult<ReturnDto>> Create(CreateReturnRequest request, CancellationToken ct)
    {
        var result = await unitReturns.RequestAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value.UnitReturnId }, result.Value)
            : Failure(result.Error);
    }

    /// <summary>Staff see every request (queue: <c>status=Approved</c>); a customer sees only their own.</summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<PagedResult<ReturnDto>>> List(
        ReturnStatus? status, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await unitReturns.ListAsync(status, page, pageSize, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpGet("{id:int}")]
    [Authorize]
    public async Task<ActionResult<ReturnDto>> Get(int id, CancellationToken ct)
    {
        var result = await unitReturns.GetAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost("{id:int}/approve")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<ReturnDto>> Approve(int id, ApproveReturnRequest request, CancellationToken ct)
    {
        var result = await unitReturns.ApproveAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost("{id:int}/inspection")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<ReturnDto>> Inspect(int id, CreateReturnInspectionRequest request, CancellationToken ct)
    {
        var result = await unitReturns.InspectAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost("{id:int}/settle-deposit")]
    [Authorize(Roles = Roles.BusinessOperationsManager)]
    public async Task<ActionResult<DepositSettlementDto>> SettleDeposit(int id, CancellationToken ct)
    {
        var result = await unitReturns.SettleAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
}
