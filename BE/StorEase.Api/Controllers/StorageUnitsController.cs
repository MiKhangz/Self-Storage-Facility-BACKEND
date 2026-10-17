using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.StorageUnits;
using StorEase.Application.Features.StorageUnits.Dtos;
using StorEase.Domain.Enums;

namespace StorEase.Api.Controllers;

[Route("api/storage-units")]
public class StorageUnitsController(IStorageUnitService units) : ApiControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<StorageUnitListItemDto>>> List(
        int? facilityId, StorageUnitStatus? status, int? unitTypeId, string? search,
        int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await units.ListAsync(facilityId, status, unitTypeId, search, page, pageSize, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<StorageUnitListItemDto>> Get(int id, CancellationToken ct)
    {
        var result = await units.GetAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost]
    [Authorize(Roles = Roles.FacilityManager + "," + Roles.SystemAdministrator)]
    public async Task<ActionResult<StorageUnitListItemDto>> Create(CreateStorageUnitRequest request, CancellationToken ct)
    {
        var result = await units.CreateAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value.StorageUnitId }, result.Value)
            : Failure(result.Error);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.FacilityManager + "," + Roles.SystemAdministrator)]
    public async Task<ActionResult<StorageUnitListItemDto>> Update(int id, UpdateStorageUnitRequest request, CancellationToken ct)
    {
        var result = await units.UpdateAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>Units are never deleted (business rule 10): an Available unit is retired instead.</summary>
    [HttpPost("{id:int}/retire")]
    [Authorize(Roles = Roles.FacilityManager + "," + Roles.SystemAdministrator)]
    public async Task<ActionResult<StorageUnitListItemDto>> Retire(int id, CancellationToken ct)
    {
        var result = await units.SetRetiredAsync(id, true, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost("{id:int}/restore")]
    [Authorize(Roles = Roles.FacilityManager + "," + Roles.SystemAdministrator)]
    public async Task<ActionResult<StorageUnitListItemDto>> Restore(int id, CancellationToken ct)
    {
        var result = await units.SetRetiredAsync(id, false, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
}
