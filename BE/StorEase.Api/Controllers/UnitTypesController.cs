using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.StorageUnits;
using StorEase.Application.Features.StorageUnits.Dtos;

namespace StorEase.Api.Controllers;

[Route("api/unit-types")]
public sealed class UnitTypesController(IUnitCatalogService catalog) : ApiControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<UnitTypeDto>>> List(CancellationToken ct)
    {
        var result = await catalog.ListUnitTypesAsync(ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<UnitTypeDto>> Get(int id, CancellationToken ct)
    {
        var result = await catalog.GetUnitTypeAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpGet("{id:int}/availability")]
    [AllowAnonymous]
    public async Task<ActionResult<UnitAvailabilityDto>> Availability(int id, int facilityId, DateOnly moveInDate, CancellationToken ct)
    {
        var result = await catalog.GetAvailabilityAsync(facilityId, id, moveInDate, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpGet("{id:int}/quote")]
    [AllowAnonymous]
    public async Task<ActionResult<UnitPriceQuoteDto>> Quote(int id, int facilityId, DateOnly moveInDate, int rentalMonths, CancellationToken ct)
    {
        var result = await catalog.QuoteAsync(facilityId, id, moveInDate, rentalMonths, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost]
    [Authorize(Roles = Roles.BusinessOperationsManager)]
    public async Task<ActionResult<UnitTypeDto>> Create(UnitTypeRequest request, CancellationToken ct)
    {
        var result = await catalog.CreateUnitTypeAsync(request, ct);
        return result.IsSuccess ? CreatedAtAction(nameof(Get), new { id = result.Value.UnitTypeId }, result.Value) : Failure(result.Error);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.BusinessOperationsManager)]
    public async Task<ActionResult<UnitTypeDto>> Update(int id, UnitTypeRequest request, CancellationToken ct)
    {
        var result = await catalog.UpdateUnitTypeAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.BusinessOperationsManager)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await catalog.DeleteUnitTypeAsync(id, ct);
        return result.IsSuccess ? NoContent() : Failure(result.Error);
    }
}
