using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.Facilities;
using StorEase.Application.Features.Facilities.Dtos;

namespace StorEase.Api.Controllers;

/// <summary>Create and list floors under <c>/api/facilities/{id}/floors</c>; edit and delete them here.</summary>
[Route("api/floors")]
[Authorize(Roles = Roles.FacilityManager + "," + Roles.SystemAdministrator)]
public class FloorsController(IFloorService floors) : ApiControllerBase
{
    [HttpPut("{id:int}")]
    public async Task<ActionResult<FacilityFloorDto>> Update(int id, FloorRequest request, CancellationToken ct)
    {
        var result = await floors.UpdateAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>Only a floor that has never held a unit can be deleted.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await floors.DeleteAsync(id, ct);
        return result.IsSuccess ? NoContent() : Failure(result.Error);
    }
}
