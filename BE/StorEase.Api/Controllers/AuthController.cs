using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Features.Auth;
using StorEase.Application.Features.Auth.Dtos;

namespace StorEase.Api.Controllers;

[Route("api/auth")]
[AllowAnonymous]
public class AuthController(IAuthService auth) : ApiControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
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
