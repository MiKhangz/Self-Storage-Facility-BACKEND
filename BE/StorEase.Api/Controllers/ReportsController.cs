using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.Reports;

namespace StorEase.Api.Controllers;

[Route("api/reports")]
[Authorize(Roles = Roles.FacilityManager + "," + Roles.BusinessOperationsManager + "," + Roles.SystemAdministrator)]
public sealed class ReportsController(IReportService reports) : ApiControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardDto>> Dashboard(int? facilityId, CancellationToken ct)
    {
        var result = await reports.DashboardAsync(facilityId, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpGet("occupancy")]
    public async Task<ActionResult<IReadOnlyList<OccupancyRowDto>>> Occupancy(int? facilityId, CancellationToken ct)
    {
        var result = await reports.OccupancyAsync(facilityId, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>Payments collected per month and per method. Defaults to the last 12 months.</summary>
    [HttpGet("revenue")]
    public async Task<ActionResult<RevenueReportDto>> Revenue(DateOnly? from, DateOnly? to, int? facilityId, CancellationToken ct)
    {
        var end = to ?? VietnamTime.Today;
        var result = await reports.RevenueAsync(from ?? end.AddMonths(-12).AddDays(1), end, facilityId, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
}
