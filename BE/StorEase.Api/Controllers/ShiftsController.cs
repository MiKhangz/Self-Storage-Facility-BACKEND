using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.Staff;
using StorEase.Application.Features.Staff.Dtos;

namespace StorEase.Api.Controllers;

[Route("api/shifts")]
public class ShiftsController(IShiftService shifts) : ApiControllerBase
{
    private const string Writers = Roles.FacilityManager + "," + Roles.SystemAdministrator;
    private const string Readers = Roles.Staff + "," + Roles.BusinessOperationsManager + "," + Roles.SystemAdministrator;

    [HttpPost]
    [Authorize(Roles = Writers)]
    public async Task<ActionResult<ShiftDto>> Create(CreateShiftRequest request, CancellationToken ct)
    {
        var result = await shifts.CreateAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value.ShiftId }, result.Value)
            : Failure(result.Error);
    }

    [HttpGet]
    [Authorize(Roles = Readers)]
    public async Task<ActionResult<PagedResult<ShiftDto>>> List(
        [FromQuery] int? facilityId, [FromQuery] int? userId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await shifts.ListAsync(facilityId, userId, from, to, page, pageSize, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = Readers)]
    public async Task<ActionResult<ShiftDto>> Get(int id, CancellationToken ct)
    {
        var result = await shifts.GetAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Writers)]
    public async Task<ActionResult<ShiftDto>> Update(int id, UpdateShiftRequest request, CancellationToken ct)
    {
        var result = await shifts.UpdateAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Writers)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await shifts.DeleteAsync(id, ct);
        return result.IsSuccess ? NoContent() : Failure(result.Error);
    }
}
