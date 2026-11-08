using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.Pricing;
using StorEase.Application.Features.Pricing.Dtos;

namespace StorEase.Api.Controllers;

/// <summary>A facility's own price list in versions. Business Operations and Admin edit; managers read their own.</summary>
[Route("api/facilities/{facilityId:int}/price-versions")]
[Authorize(Roles = Editors)]
public sealed class FacilityPricesController(IFacilityPricingService prices) : ApiControllerBase
{
    private const string Editors = Roles.BusinessOperationsManager + "," + Roles.SystemAdministrator;

    [HttpGet]
    [Authorize(Roles = Editors + "," + Roles.FacilityManager)]
    public async Task<ActionResult<FacilityPriceListDto>> Get(int facilityId, CancellationToken ct)
    {
        var result = await prices.GetAsync(facilityId, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost]
    public async Task<ActionResult<FacilityPriceVersionDto>> CreateDraft(int facilityId, CancellationToken ct)
    {
        var result = await prices.CreateDraftAsync(facilityId, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPut("{versionId:int}/unit-prices/{unitTypeId:int}")]
    public async Task<ActionResult<FacilityPriceVersionDto>> SetPrice(int facilityId, int versionId, int unitTypeId, UpsertUnitPriceRequest request, CancellationToken ct)
    {
        var result = await prices.SetPriceAsync(facilityId, versionId, unitTypeId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpDelete("{versionId:int}/unit-prices/{unitTypeId:int}")]
    public async Task<ActionResult<FacilityPriceVersionDto>> RemovePrice(int facilityId, int versionId, int unitTypeId, CancellationToken ct)
    {
        var result = await prices.RemovePriceAsync(facilityId, versionId, unitTypeId, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost("{versionId:int}/publish")]
    public async Task<ActionResult<FacilityPriceVersionDto>> Publish(int facilityId, int versionId, CancellationToken ct)
    {
        var result = await prices.PublishAsync(facilityId, versionId, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpDelete("{versionId:int}")]
    public async Task<IActionResult> DeleteDraft(int facilityId, int versionId, CancellationToken ct)
    {
        var result = await prices.DeleteDraftAsync(facilityId, versionId, ct);
        return result.IsSuccess ? NoContent() : Failure(result.Error);
    }
}
