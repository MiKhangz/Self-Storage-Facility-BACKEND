using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.CheckIns;

namespace StorEase.Api.Controllers;

/// <summary>SR-05: a customer's visits to their unit, recorded by staff at reception.</summary>
[Route("api/check-ins")]
public class CheckInsController(ICheckInService visits) : ApiControllerBase
{
    [HttpPost("check-in")]
    [Authorize(Roles = Roles.Staff)]
    [ProducesResponseType<CheckInDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CheckInDto>> CheckIn(CheckInRequest request, CancellationToken ct)
    {
        var result = await visits.CheckInAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value.CheckInId }, result.Value)
            : Failure(result.Error);
    }

    [HttpPost("{id:int}/check-out")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<CheckInDto>> CheckOut(int id, CancellationToken ct)
    {
        var result = await visits.CheckOutAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>The customer left without checking out: staff close the visit with when they left and why.</summary>
    [HttpPost("{id:int}/mark-left")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<CheckInDto>> MarkLeft(int id, MarkLeftRequest request, CancellationToken ct)
    {
        var result = await visits.MarkLeftAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>Staff see visits at their facilities; a customer only their own.</summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<PagedResult<CheckInDto>>> List(int? contractId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await visits.ListAsync(contractId, page, pageSize, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>The check-in screen: everyone renting at the caller's facilities, those inside first.</summary>
    [HttpGet("board")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<IReadOnlyList<CheckInBoardRowDto>>> Board(CancellationToken ct)
    {
        var result = await visits.BoardAsync(ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>Finds the renter by the access code (PIN) they say at the desk; 404 when no working code matches.</summary>
    [HttpPost("find")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<CheckInBoardRowDto>> Find(FindByAccessCodeRequest request, CancellationToken ct)
    {
        var result = await visits.FindByAccessCodeAsync(request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpGet("{id:int}")]
    [Authorize]
    public async Task<ActionResult<CheckInDto>> Get(int id, CancellationToken ct)
    {
        var result = await visits.GetAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
}
