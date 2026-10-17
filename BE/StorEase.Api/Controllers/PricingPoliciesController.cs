using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.Pricing;
using StorEase.Application.Features.Pricing.Dtos;

namespace StorEase.Api.Controllers;

[Route("api/pricing-policies")]
[Authorize(Roles = Roles.BusinessOperationsManager)]
public sealed class PricingPoliciesController(IPricingService pricing) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<PricingPolicyDto>>> List(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await pricing.ListPoliciesAsync(page, pageSize, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>The terms customers agree to when booking (deposit, late fee, notice, discounts). No sign-in needed.</summary>
    [HttpGet("current-terms")]
    [AllowAnonymous]
    public async Task<ActionResult<PolicyTermsDto>> CurrentTerms(CancellationToken ct)
    {
        var result = await pricing.GetCurrentTermsAsync(ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PricingPolicyDto>> Get(int id, CancellationToken ct)
    {
        var result = await pricing.GetPolicyAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost]
    public async Task<ActionResult<PricingPolicyDto>> Create(CreatePricingPolicyRequest request, CancellationToken ct)
    {
        var result = await pricing.CreatePolicyAsync(request, ct);
        return result.IsSuccess ? CreatedAtAction(nameof(Get), new { id = result.Value.PricingPolicyId }, result.Value) : Failure(result.Error);
    }

    [HttpPost("{id:int}/activate")]
    public async Task<ActionResult<PricingPolicyDto>> Activate(int id, CancellationToken ct)
    {
        var result = await pricing.ActivatePolicyAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPut("{id:int}/unit-prices/{unitTypeId:int}")]
    public async Task<ActionResult<UnitPriceDto>> UpsertUnitPrice(int id, int unitTypeId, UpsertUnitPriceRequest request, CancellationToken ct)
    {
        var result = await pricing.UpsertUnitPriceAsync(id, unitTypeId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost("{id:int}/fee-types")]
    public async Task<ActionResult<FeeTypeDto>> CreateFeeType(int id, FeeTypeRequest request, CancellationToken ct)
    {
        var result = await pricing.CreateFeeTypeAsync(id, request, ct);
        return result.IsSuccess ? CreatedAtAction(nameof(Get), new { id }, result.Value) : Failure(result.Error);
    }

    [HttpPut("~/api/fee-types/{feeTypeId:int}")]
    public async Task<ActionResult<FeeTypeDto>> UpdateFeeType(int feeTypeId, FeeTypeRequest request, CancellationToken ct)
    {
        var result = await pricing.UpdateFeeTypeAsync(feeTypeId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpDelete("~/api/fee-types/{feeTypeId:int}")]
    public async Task<IActionResult> DeleteFeeType(int feeTypeId, CancellationToken ct)
    {
        var result = await pricing.DeleteFeeTypeAsync(feeTypeId, ct);
        return result.IsSuccess ? NoContent() : Failure(result.Error);
    }
}
