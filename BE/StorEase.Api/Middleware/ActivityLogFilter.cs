using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
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
        // A sign-in is logged once, by the service; here it would repeat whenever the browser still held a session.
        if (action is { ControllerName: "Auth", ActionName: "Login" })
            return;
        var entry = new ActivityEntry(
            userId,
            null,
            $"{http.Request.Method} {action?.ControllerName}.{action?.ActionName}",
            action?.ControllerName,
            context.RouteData.Values.TryGetValue("id", out var id) ? id?.ToString()
                : IdFromResponse(action?.ControllerName, (context.Result as ObjectResult)?.Value),
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

    /// <summary>
    /// A create has no id in its route, so the record it made is read from the response: the property named after
    /// the controller (Reservations → ReservationId, Facilities → FacilityId), else the first int ending in "Id".
    /// </summary>
    public static string? IdFromResponse(string? controller, object? value)
    {
        if (value is null || string.IsNullOrEmpty(controller))
            return null;
        var singular = controller.EndsWith("ies") ? controller[..^3] + "y" : controller.TrimEnd('s');
        var ids = value.GetType().GetProperties().Where(p => p.PropertyType == typeof(int) && p.Name.EndsWith("Id")).ToList();
        var match = ids.FirstOrDefault(p => p.Name == singular + "Id") ?? ids.FirstOrDefault();
        return match?.GetValue(value)?.ToString();
    }
}
