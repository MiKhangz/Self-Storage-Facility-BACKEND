using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.MoveOuts;
using StorEase.Application.Features.MoveOuts.Dtos;
using StorEase.Domain.Enums;

namespace StorEase.Api.Controllers;

[Route("api/move-out-requests")]
public class MoveOutRequestsController(IMoveOutService moveOuts) : ApiControllerBase
{
    [HttpPost]
    [Authorize(Roles = Roles.StorageCustomer)]
    public async Task<ActionResult<MoveOutRequestDto>> Create(CreateMoveOutRequest request, CancellationToken ct)
    {
        var result = await moveOuts.RequestAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value.MoveOutRequestId }, result.Value)
            : Failure(result.Error);
    }

    /// <summary>Staff see every request (queue: <c>status=Approved</c>); a customer sees only their own.</summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<PagedResult<MoveOutRequestDto>>> List(
        MoveOutRequestStatus? status, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await moveOuts.ListAsync(status, page, pageSize, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpGet("{id:int}")]
    [Authorize]
    public async Task<ActionResult<MoveOutRequestDto>> Get(int id, CancellationToken ct)
    {
        var result = await moveOuts.GetAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost("{id:int}/approve")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<MoveOutRequestDto>> Approve(int id, ApproveMoveOutRequest request, CancellationToken ct)
    {
        var result = await moveOuts.ApproveAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost("{id:int}/inspection")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<MoveOutRequestDto>> Inspect(int id, CreateReturnInspectionRequest request, CancellationToken ct)
    {
        var result = await moveOuts.InspectAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost("{id:int}/settle-deposit")]
    [Authorize(Roles = Roles.BusinessOperationsManager)]
    public async Task<ActionResult<DepositSettlementDto>> SettleDeposit(int id, CancellationToken ct)
    {
        var result = await moveOuts.SettleAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
}
