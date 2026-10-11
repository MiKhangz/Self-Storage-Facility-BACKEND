using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.Renewals;
using StorEase.Application.Features.Renewals.Dtos;

namespace StorEase.Api.Controllers;

[Route("api/renewals")]
public class RenewalsController(IRenewalService renewals) : ApiControllerBase
{
    [HttpPost]
    [Authorize(Roles = Roles.StorageCustomer)]
    public async Task<ActionResult<RenewalDto>> Create(CreateRenewalRequest request, CancellationToken ct)
    {
        var result = await renewals.RequestAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value.RenewalId }, result.Value)
            : Failure(result.Error);
    }

    /// <summary>Staff see every renewal; a customer sees only those of their own contracts.</summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<PagedResult<RenewalDto>>> List(
        int? contractId, string? status, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await renewals.ListAsync(contractId, status, page, pageSize, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpGet("{id:int}")]
    [Authorize]
    public async Task<ActionResult<RenewalDto>> Get(int id, CancellationToken ct)
    {
        var result = await renewals.GetAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
}
