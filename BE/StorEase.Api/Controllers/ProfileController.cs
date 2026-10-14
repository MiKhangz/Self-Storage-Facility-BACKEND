using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Features.Users;

namespace StorEase.Api.Controllers;

/// <summary>The signed-in user's own account (any role).</summary>
[Route("api/users/me")]
[Authorize]
public class ProfileController(IProfileService profile) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ProfileDto>> Get(CancellationToken ct)
    {
        var result = await profile.GetAsync(ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPut]
    public async Task<ActionResult<ProfileDto>> Update(UpdateProfileRequest request, CancellationToken ct)
    {
        var result = await profile.UpdateAsync(request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost("password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        var result = await profile.ChangePasswordAsync(request, ct);
        return result.IsSuccess ? NoContent() : Failure(result.Error);
    }

    [HttpGet("notification-settings")]
    public async Task<ActionResult<NotificationSettingsDto>> GetNotificationSettings(CancellationToken ct)
    {
        var result = await profile.GetNotificationSettingsAsync(ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPut("notification-settings")]
    public async Task<ActionResult<NotificationSettingsDto>> UpdateNotificationSettings(NotificationSettingsDto request, CancellationToken ct)
    {
        var result = await profile.UpdateNotificationSettingsAsync(request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
}
