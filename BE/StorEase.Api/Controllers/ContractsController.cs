using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.AccessCodes;
using StorEase.Application.Features.AccessCodes.Dtos;
using StorEase.Application.Features.Contracts;
using StorEase.Application.Features.Contracts.Dtos;
using StorEase.Domain.Enums;

namespace StorEase.Api.Controllers;

[Route("api/contracts")]
public class ContractsController(IContractService contracts, IAccessCodeService accessCodes) : ApiControllerBase
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

    [HttpGet("export")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> Export(ContractStatus? status, CancellationToken ct)
    {
        var result = await contracts.ExportCsvAsync(status, ct);
        if (!result.IsSuccess)
            return Failure(result.Error);

        // The BOM makes Excel read the Vietnamese names as UTF-8.
        var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(result.Value)).ToArray();
        return File(bytes, "text/csv", "contracts.csv");
    }

    [HttpPost("{id:int}/terminate")]
    [Authorize(Roles = Roles.FacilityManager)]
    public async Task<ActionResult<ContractDto>> Terminate(int id, CancellationToken ct)
    {
        var result = await contracts.TerminateAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost("{id:int}/access-codes")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<AccessCodeDto>> IssueAccessCode(int id, CancellationToken ct)
    {
        var result = await accessCodes.IssueAsync(id, ct);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : Failure(result.Error);
    }
}
