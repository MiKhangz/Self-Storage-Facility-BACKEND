using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.Facilities;
using StorEase.Application.Features.Facilities.Dtos;

namespace StorEase.Api.Controllers;

[Route("api/facilities")]
public class FacilitiesController(IFacilityService facilities) : ApiControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<FacilityListItemDto>>> List(
        int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await facilities.ListAsync(page, pageSize, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
}
