using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;

namespace StorEase.Application.Features.Notifications;

public sealed record NotificationDto(int NotificationId, string Title, string Body, string Link, DateTime CreatedAt, DateTime? ReadAt);

public sealed record NotificationListDto(IReadOnlyList<NotificationDto> Items, int UnreadCount);

/// <summary>The newest notification the customer was shown; one that arrived after it stays unread.</summary>
public sealed record ReadNotificationsRequest(int UpToId);

public interface INotificationService
{
    Task<Result<NotificationListDto>> ListMineAsync(CancellationToken ct);
    Task<Result<bool>> MarkReadAsync(ReadNotificationsRequest request, CancellationToken ct);
}

public sealed class NotificationService(IUnitOfWork uow, ICurrentUser currentUser) : INotificationService
{
    // shortcut: the bell shows the newest 20 and has no paging; add a page if customers need older ones.
    private const int Shown = 20;

    /// <summary>
    /// The caller saves. Only for messages a signed-in customer may read: never sign-in, reset or verification links.
    /// </summary>
    internal static void Add(IUnitOfWork uow, int userId, string title, string body, string link) =>
        uow.Repository<Notification>().Add(new Notification { UserId = userId, Title = title, Body = body, Link = link, CreatedAt = DateTime.UtcNow });

    public async Task<Result<NotificationListDto>> ListMineAsync(CancellationToken ct)
    {
        var mine = Mine();
        var items = await mine.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.NotificationId).Take(Shown)
            .Select(n => new NotificationDto(n.NotificationId, n.Title, n.Body, n.Link, n.CreatedAt, n.ReadAt)).ToListAsync(ct);
        return new NotificationListDto(items, await mine.CountAsync(n => n.ReadAt == null, ct));
    }

    public async Task<Result<bool>> MarkReadAsync(ReadNotificationsRequest request, CancellationToken ct)
    {
        var unread = await Mine().Where(n => n.ReadAt == null && n.NotificationId <= request.UpToId).ToListAsync(ct);
        foreach (var notification in unread)
            notification.ReadAt = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct);
        return true;
    }

    private IQueryable<Notification> Mine()
    {
        var userId = currentUser.UserId;
        return uow.Repository<Notification>().Query().Where(n => n.UserId == userId);
    }
}
