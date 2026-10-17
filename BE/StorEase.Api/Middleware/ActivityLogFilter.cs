using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using StorEase.Application.Features.ActivityLogs;

namespace StorEase.Api.Middleware;

/// <summary>
/// Audit trail (BE-70): every write by a signed-in user is recorded with its outcome. Reads are not logged, and
/// anonymous writes (login) log themselves in the service, where the user is known.
/// </summary>
public class ActivityLogFilter(IServiceScopeFactory scopes, ILogger<ActivityLogFilter> logger) : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        await next();

        var http = context.HttpContext;
        if (HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method) || HttpMethods.IsOptions(http.Request.Method)
            || !int.TryParse(http.User.FindFirst("sub")?.Value, out var userId))
            return;

        var action = context.ActionDescriptor as ControllerActionDescriptor;
        var entry = new ActivityEntry(
            userId,
            null,
            $"{http.Request.Method} {action?.ControllerName}.{action?.ActionName}",
            action?.ControllerName,
            context.RouteData.Values.TryGetValue("id", out var id) ? id?.ToString() : null,
            http.Connection.RemoteIpAddress?.ToString(),
            http.Response.StatusCode < 400);
        try
        {
            // A fresh scope, so a failed request's unsaved changes in the shared DbContext are never saved with the log.
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IActivityLogService>().RecordAsync(entry, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // The write itself already happened; losing one audit row must not turn it into a 500.
            logger.LogError(ex, "Could not record activity {Action} for user {UserId}", entry.Action, userId);
        }
    }
}
