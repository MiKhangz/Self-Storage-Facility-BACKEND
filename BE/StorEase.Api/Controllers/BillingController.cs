using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.Billing;

namespace StorEase.Api.Controllers;

[Route("api/billing")]
[Authorize(Roles = Roles.BusinessOperationsManager + "," + Roles.SystemAdministrator)]
public sealed class BillingController(IBillingCycleService billing) : ApiControllerBase
{
    /// <summary>Runs the daily billing job now (it also runs on its own every hour). <c>date</c> defaults to today.</summary>
    [HttpPost("run")]
    public async Task<ActionResult<BillingRunDto>> Run(DateOnly? date, CancellationToken ct)
    {
        var result = await billing.RunAsync(date ?? VietnamTime.Today, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
}
