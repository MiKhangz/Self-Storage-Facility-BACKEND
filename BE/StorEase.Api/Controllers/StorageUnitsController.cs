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
        int? facilityId, StorageUnitStatus? status, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await units.ListAsync(facilityId, status, page, pageSize, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
}
