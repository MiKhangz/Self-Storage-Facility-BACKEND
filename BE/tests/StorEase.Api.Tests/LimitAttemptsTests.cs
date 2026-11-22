using System.Globalization;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using StorEase.Api.Middleware;
using StorEase.Application.Common;

namespace StorEase.Api.Tests;

public class LimitAttemptsTests
{
    private static async Task<int?> Attempt(LimitAttemptsAttribute limit, int userId)
    {
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", userId.ToString())])) };
        var action = new ActionContext(http, new RouteData(), new ActionDescriptor());
        var context = new ActionExecutingContext(action, [], new Dictionary<string, object?>(), new object());
        await limit.OnActionExecutionAsync(context, () => Task.FromResult(new ActionExecutedContext(action, [], new object())));
        return (context.Result as ObjectResult)?.StatusCode;
    }

    [Fact]
    public async Task An_account_out_of_attempts_is_refused_and_others_are_not()
    {
        var limit = new LimitAttemptsAttribute(2, 15);

        (await Attempt(limit, 1)).Should().BeNull();
        (await Attempt(limit, 1)).Should().BeNull();
        (await Attempt(limit, 1)).Should().Be(StatusCodes.Status429TooManyRequests);
        (await Attempt(limit, 2)).Should().BeNull();
    }

    [Fact]
    public void The_refusal_is_translated()
    {
        var before = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo("vi");
        try { ErrorText.Localize(AuthLimits.Message).Should().NotBe(AuthLimits.Message); }
        finally { CultureInfo.CurrentUICulture = before; }
    }
}
