using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.Notifications;

namespace StorEase.Api.Controllers;

/// <summary>The signed-in customer's own notifications.</summary>
[Route("api/notifications")]
[Authorize(Roles = Roles.StorageCustomer)]
public class NotificationsController(INotificationService notifications) : ApiControllerBase
{
    /// <summary>The newest 20, and how many are unread in all.</summary>
    [HttpGet]
    public async Task<ActionResult<NotificationListDto>> List(CancellationToken ct)
    {
        var result = await notifications.ListMineAsync(ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>Marks those up to the given id as read.</summary>
    [HttpPost("read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkRead(ReadNotificationsRequest request, CancellationToken ct)
    {
        var result = await notifications.MarkReadAsync(request, ct);
        return result.IsSuccess ? NoContent() : Failure(result.Error);
    }
}
