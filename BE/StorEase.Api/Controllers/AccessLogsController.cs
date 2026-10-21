using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.AccessCodes;
using StorEase.Application.Features.AccessCodes.Dtos;

namespace StorEase.Api.Controllers;

[Route("api/access-logs")]
public class AccessLogsController(IAccessLogService logs) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Roles = Roles.FacilityManager + "," + Roles.BusinessOperationsManager + "," + Roles.SystemAdministrator)]
    public async Task<ActionResult<PagedResult<AccessLogDto>>> Search(
        int? storageUnitId, int? customerId, DateOnly? from, DateOnly? to,
        int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await logs.SearchAsync(storageUnitId, customerId, from, to, page, pageSize, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpGet("mine")]
    [Authorize(Roles = Roles.StorageCustomer)]
    public async Task<ActionResult<PagedResult<AccessLogDto>>> Mine(
        DateOnly? from, DateOnly? to, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await logs.ListMineAsync(from, to, page, pageSize, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
}
