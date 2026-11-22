using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Auth.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;

namespace StorEase.Api.Middleware;

/// <summary>Limits on the public sign-in, register and password routes.</summary>
public static class AuthLimits
{
    public const string PerAddress = "auth";
    public const string Message = "Too many attempts. Please wait a few minutes and try again.";

    public static IServiceCollection AddAuthLimits(this IServiceCollection services) => services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        // Guards the CPU (every guess is a slow hash); the guessing itself is limited per account below.
        // shortcut: the address is the caller's socket, so behind a proxy everyone shares it; add ForwardedHeaders when deployed behind one.
        options.AddPolicy(PerAddress, http => RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1) }));
        options.OnRejected = async (context, ct) =>
            await context.HttpContext.Response.WriteAsJsonAsync(
                Refusal(context.HttpContext, context.Lease), options: null, contentType: "application/problem+json", ct);
    });

    internal static ProblemDetails Refusal(HttpContext http, RateLimitLease lease)
    {
        if (lease.TryGetMetadata(MetadataName.RetryAfter, out var wait))
            http.Response.Headers.RetryAfter = Math.Max(1, (int)wait.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        return new ProblemDetails { Status = StatusCodes.Status429TooManyRequests, Title = ErrorText.Localize(Message) };
    }
}

/// <summary>
/// So many attempts per account in a fixed window. The account is the one the database finds for the typed email:
/// SQL Server treats other spellings (case, full-width letters, trailing spaces) as the same address. An email
/// without an account is limited the same way, so a refusal does not reveal who has one.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class LimitAttemptsAttribute(int attempts, int minutes) : ActionFilterAttribute
{
    /// <summary>Count each address apart, so a stranger's guesses do not lock the owner out.</summary>
    public bool PerAddress { get; init; }

    // MVC keeps one instance per action, so the counts live as long as the app.
    private readonly PartitionedRateLimiter<string> limiter = PartitionedRateLimiter.Create<string, string>(key =>
        RateLimitPartition.GetFixedWindowLimiter(key,
            _ => new FixedWindowRateLimiterOptions { PermitLimit = attempts, Window = TimeSpan.FromMinutes(minutes) }));

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        var key = await AccountAsync(context);
        if (PerAddress)
            key += $"|{http.Connection.RemoteIpAddress}";
        using var lease = limiter.AttemptAcquire(key);
        if (lease.IsAcquired)
            await next();
        else
            context.Result = new ObjectResult(AuthLimits.Refusal(http, lease)) { StatusCode = StatusCodes.Status429TooManyRequests };
    }

    private static async Task<string> AccountAsync(ActionExecutingContext context)
    {
        var http = context.HttpContext;
        var typed = context.ActionArguments.Values.Select(argument => argument switch
        {
            LoginRequest r => r.Email,
            ForgotPasswordRequest r => r.Email,
            ResendVerificationRequest r => r.Email,
            _ => null,
        }).FirstOrDefault(email => email is not null);
        if (typed is null)
            return $"#{http.User.FindFirst("sub")?.Value}"; // signed in: changing their own password

        var email = typed.Trim().ToLowerInvariant();
        var userId = await http.RequestServices.GetRequiredService<IUnitOfWork>().Repository<User>().Query()
            .Where(u => u.Email == email).Select(u => (int?)u.UserId).FirstOrDefaultAsync(http.RequestAborted);
        return userId is { } id ? $"#{id}" : email;
    }
}
