using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.ActivityLogs;

namespace StorEase.Api.Controllers;

[Route("api/activity-logs")]
[Authorize(Roles = Roles.SystemAdministrator)]
public sealed class ActivityLogsController(IActivityLogService logs) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ActivityLogDto>>> List(
        int? userId, string? action, DateOnly? from, DateOnly? to, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var result = await logs.ListAsync(userId, action, from, to, page, pageSize, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
}
