using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Features.Auth;
using StorEase.Application.Features.Auth.Dtos;

namespace StorEase.Api.Controllers;

[Route("api/auth")]
[AllowAnonymous]
public class AuthController(IAuthService auth) : ApiControllerBase
{
    /// <summary>Holds the login token. HttpOnly, so page scripts never see it; it lasts as long as the token inside.</summary>
    public const string SessionCookie = "storease_session";

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        if (!result.IsSuccess)
            return Failure(result.Error);
        // Strict: the browser never sends it on a request started by another site, which is the CSRF guard.
        Response.Cookies.Append(SessionCookie, result.Value!.Token, new CookieOptions
        {
            HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = "/api",
        });
        return Ok(result.Value);
    }

    /// <summary>The signed-in user, so a reloaded page can pick its session back up from the cookie.</summary>
    [HttpGet("session")]
    public ActionResult<SessionResponse> Session()
    {
        // The controller is [AllowAnonymous], which would override [Authorize] here.
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized();
        var exp = long.Parse(User.FindFirstValue("exp")!);
        return new SessionResponse(
            int.Parse(User.FindFirstValue("sub")!), User.FindFirstValue("name") ?? "", User.FindFirstValue("role")!,
            DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime);
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(SessionCookie, new CookieOptions { Secure = true, SameSite = SameSiteMode.Strict, Path = "/api" });
        return NoContent();
    }

    /// <summary>Creates a customer account in Pending and emails a verification link.</summary>
    [HttpPost("register")]
    [ProducesResponseType<RegisterResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<RegisterResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await auth.RegisterAsync(request, ct);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : Failure(result.Error);
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request, CancellationToken ct)
    {
        var result = await auth.VerifyEmailAsync(request, ct);
        return result.IsSuccess ? NoContent() : Failure(result.Error);
    }

    /// <summary>Always 204, whether or not the email has an account.</summary>
    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification(ResendVerificationRequest request, CancellationToken ct)
    {
        var result = await auth.ResendVerificationAsync(request, ct);
        return result.IsSuccess ? NoContent() : Failure(result.Error);
    }

    /// <summary>Always 204, whether or not the email has an account.</summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        var result = await auth.ForgotPasswordAsync(request, ct);
        return result.IsSuccess ? NoContent() : Failure(result.Error);
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        var result = await auth.ResetPasswordAsync(request, ct);
        return result.IsSuccess ? NoContent() : Failure(result.Error);
    }
}
