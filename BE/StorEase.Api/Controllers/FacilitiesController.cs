using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.Facilities;
using StorEase.Application.Features.Facilities.Dtos;

namespace StorEase.Api.Controllers;

[Route("api/facilities")]
public class FacilitiesController(IFacilityService facilities, IFloorService floors) : ApiControllerBase
{
    /// <summary>Search by name, address or code; filter by district. <c>includeInactive</c> only works for administrators.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<FacilityListItemDto>>> List(
        string? search, string? district, bool includeInactive = false, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await facilities.ListAsync(search, district, includeInactive, page, pageSize, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<FacilityDetailDto>> Get(int id, CancellationToken ct)
    {
        var result = await facilities.GetAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost]
    [Authorize(Roles = Roles.SystemAdministrator)]
    public async Task<ActionResult<FacilityDetailDto>> Create(FacilityRequest request, CancellationToken ct)
    {
        var result = await facilities.CreateAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value.FacilityId }, result.Value)
            : Failure(result.Error);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.SystemAdministrator)]
    public async Task<ActionResult<FacilityDetailDto>> Update(int id, FacilityRequest request, CancellationToken ct)
    {
        var result = await facilities.UpdateAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>Facilities are never deleted (business rule 10); 409 while units are reserved/occupied.</summary>
    [HttpPost("{id:int}/deactivate")]
    [Authorize(Roles = Roles.SystemAdministrator)]
    public async Task<ActionResult<FacilityDetailDto>> Deactivate(int id, CancellationToken ct)
    {
        var result = await facilities.SetActiveAsync(id, false, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost("{id:int}/activate")]
    [Authorize(Roles = Roles.SystemAdministrator)]
    public async Task<ActionResult<FacilityDetailDto>> Activate(int id, CancellationToken ct)
    {
        var result = await facilities.SetActiveAsync(id, true, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>Every unit by floor with its status, for the colour-coded board.</summary>
    [HttpGet("{id:int}/unit-board")]
    [Authorize(Roles = Roles.Staff + "," + Roles.BusinessOperationsManager + "," + Roles.SystemAdministrator)]
    public async Task<ActionResult<UnitBoardDto>> UnitBoard(int id, CancellationToken ct)
    {
        var result = await facilities.GetUnitBoardAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpGet("{id:int}/floors")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<FacilityFloorDto>>> Floors(int id, CancellationToken ct)
    {
        var result = await floors.ListAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost("{id:int}/floors")]
    [Authorize(Roles = Roles.FacilityManager + "," + Roles.SystemAdministrator)]
    [ProducesResponseType<FacilityFloorDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<FacilityFloorDto>> CreateFloor(int id, FloorRequest request, CancellationToken ct)
    {
        var result = await floors.CreateAsync(id, request, ct);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : Failure(result.Error);
    }
}
