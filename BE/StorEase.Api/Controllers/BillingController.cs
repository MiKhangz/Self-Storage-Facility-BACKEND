using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.Billing;

namespace StorEase.Api.Controllers;

[Route("api/billing")]
[Authorize(Roles = Roles.BusinessOperationsManager + "," + Roles.SystemAdministrator)]
public sealed class BillingController(IBillingCycleService billing, IHostEnvironment environment, IConfiguration config) : ApiControllerBase
{
    /// <summary>
    /// Runs the daily billing job now (it also runs on its own every hour). <c>date</c> defaults to today, and can be
    /// another day only in Development with Billing:AllowOtherDates on, as the E2E tests run.
    /// </summary>
    [HttpPost("run")]
    public async Task<ActionResult<BillingRunDto>> Run(DateOnly? date, CancellationToken ct)
    {
        var today = VietnamTime.Today;
        // A future date charges and locks out customers who owe nothing yet, and nothing undoes it. Development alone
        // is not enough: the demo database runs there too.
        if (date is { } day && day != today && !(environment.IsDevelopment() && config.GetValue("Billing:AllowOtherDates", false)))
            return Failure(Error.Validation("Billing can only be run for today."));
        var result = await billing.RunAsync(date ?? today, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
}
