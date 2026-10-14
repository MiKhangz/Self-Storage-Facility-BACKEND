using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.OverdueCases;
using StorEase.Domain.Enums;

namespace StorEase.Api.Controllers;

[Route("api/overdue-cases")]
[Authorize(Roles = Roles.Staff + "," + Roles.BusinessOperationsManager + "," + Roles.SystemAdministrator)]
public sealed class OverdueCasesController(IOverdueService overdue) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<OverdueCaseDto>>> List(
        OverdueStage? stage, bool openOnly = true, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await overdue.ListAsync(stage, openOnly, page, pageSize, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OverdueCaseDto>> Get(int id, CancellationToken ct)
    {
        var result = await overdue.GetAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost("{id:int}/lockout")]
    [Authorize(Roles = Roles.FacilityManager + "," + Roles.BusinessOperationsManager)]
    public async Task<ActionResult<OverdueCaseDto>> LockOut(int id, CancellationToken ct)
    {
        var result = await overdue.LockOutAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost("{id:int}/restore-access")]
    [Authorize(Roles = Roles.FacilityManager + "," + Roles.BusinessOperationsManager)]
    public async Task<ActionResult<OverdueCaseDto>> RestoreAccess(int id, CancellationToken ct)
    {
        var result = await overdue.RestoreAccessAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
}
