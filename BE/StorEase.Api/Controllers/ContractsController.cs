using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.Contracts;
using StorEase.Application.Features.Contracts.Dtos;
using StorEase.Domain.Enums;

namespace StorEase.Api.Controllers;

[Route("api/contracts")]
public class ContractsController(IContractService contracts) : ApiControllerBase
{
    /// <summary>Staff see every contract; a customer sees only their own.</summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<PagedResult<ContractDto>>> List(
        ContractStatus? status, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await contracts.ListAsync(status, page, pageSize, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpGet("{id:int}")]
    [Authorize]
    public async Task<ActionResult<ContractDto>> Get(int id, CancellationToken ct)
    {
        var result = await contracts.GetAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost("{id:int}/sign")]
    [Authorize(Roles = Roles.StorageCustomer)]
    public async Task<ActionResult<ContractDto>> Sign(int id, CancellationToken ct)
    {
        var result = await contracts.SignAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
}
