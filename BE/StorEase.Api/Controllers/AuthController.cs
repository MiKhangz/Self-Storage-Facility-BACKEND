using System.Text.RegularExpressions;
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
    /// <summary>
    /// The login token lives in an HttpOnly cookie, so page scripts never see it; it lasts as long as the token inside.
    /// The customer site and the staff portal each keep their own (the web app names its side in X-Portal), as two
    /// sites on separate domains would, so a customer tab and a staff tab in one browser do not overwrite each other.
    /// </summary>
    public static string SessionCookie(HttpRequest request) =>
        request.Headers["X-Portal"] == "staff" ? "storease_staff" : "storease_customer";

    /// <summary>
    /// Cookies belong to the browser, not the window, so two people signed in on one side would share a session and
    /// act as whoever signed in last. Each window names itself in X-Tab and keeps its own cookie; a new window has
    /// none yet and starts from the shared one, the latest sign-in.
    /// shortcut: a window closed without logging out leaves its cookie until the browser closes; expire them at login if headers grow.
    /// </summary>
    public static string? TabCookie(HttpRequest request)
    {
        var tab = request.Headers["X-Tab"].ToString();
        return TabId.IsMatch(tab) ? $"{SessionCookie(request)}_{tab}" : null;
    }

    public static string? SessionToken(HttpRequest request) =>
        (TabCookie(request) is { } name ? request.Cookies[name] : null) ?? request.Cookies[SessionCookie(request)];

    private static readonly Regex TabId = new("^[a-f0-9]{16}$", RegexOptions.Compiled);

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        if (!result.IsSuccess)
            return Failure(result.Error);
        // Strict: the browser never sends it on a request started by another site, which is the CSRF guard.
        var options = new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = "/api" };
        Response.Cookies.Append(SessionCookie(Request), result.Value!.Token, options);
        if (TabCookie(Request) is { } tab)
            Response.Cookies.Append(tab, result.Value.Token, options);
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
        var options = new CookieOptions { Secure = true, SameSite = SameSiteMode.Strict, Path = "/api" };
        var tab = TabCookie(Request);
        var mine = tab is null ? null : Request.Cookies[tab];
        if (tab is not null)
            Response.Cookies.Delete(tab, options);
        // The shared cookie may be another window's sign-in; only this window's own session ends.
        if (mine is null || mine == Request.Cookies[SessionCookie(Request)])
            Response.Cookies.Delete(SessionCookie(Request), options);
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
