using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;

namespace StorEase.Api.Middleware;

/// <summary>Turns any unhandled exception into a 500 ProblemDetails. Expected failures never reach here — services return Result.</summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(
                new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = ErrorText.Localize("An unexpected error occurred."),
                    Instance = context.Request.Path,
                    Extensions = { ["traceId"] = context.TraceIdentifier },
                },
                options: null,
                contentType: "application/problem+json");
        }
    }
}
