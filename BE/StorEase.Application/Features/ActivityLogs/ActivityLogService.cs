using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.ActivityLogs;

public sealed record ActivityEntry(
    int UserId, int? FacilityId, string Action, string? EntityName, string? EntityId, string? IpAddress, bool Succeeded);

public sealed record ActivityLogDto(
    long LogId, int UserId, string UserName, string UserEmail, int? FacilityId, string Action,
    string? EntityName, string? EntityId, string? IpAddress, ActivityResult Result, DateTime CreatedAt);

public interface IActivityLogService
{
    /// <summary>Saves one audit row. Called by the API's activity filter after every write, and by login.</summary>
    Task RecordAsync(ActivityEntry entry, CancellationToken ct);

    Task<Result<PagedResult<ActivityLogDto>>> ListAsync(
        int? userId, string? action, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct);
}

public class ActivityLogService(IUnitOfWork uow) : IActivityLogService
{
    public async Task RecordAsync(ActivityEntry entry, CancellationToken ct)
    {
        uow.Repository<ActivityLog>().Add(new ActivityLog
        {
            UserId = entry.UserId,
            FacilityId = entry.FacilityId,
            Action = Truncate(entry.Action, 50)!,
            EntityName = Truncate(entry.EntityName, 50),
            EntityId = Truncate(entry.EntityId, 30),
            IpAddress = Truncate(entry.IpAddress, 45),
            Result = entry.Succeeded ? ActivityResult.Success : ActivityResult.Failure,
            CreatedAt = DateTime.UtcNow,
        });
        await uow.SaveChangesAsync(ct);
    }

    public async Task<Result<PagedResult<ActivityLogDto>>> ListAsync(
        int? userId, string? action, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct)
    {
        var query = uow.Repository<ActivityLog>().Query();
        if (userId is not null)
            query = query.Where(l => l.UserId == userId);
        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(l => l.Action.Contains(action));
        if (from is not null)
        {
            var start = from.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(l => l.CreatedAt >= start);
        }
        if (to is not null)
        {
            var end = to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(l => l.CreatedAt < end);
        }

        return await query.OrderByDescending(l => l.LogId)
            .Select(l => new ActivityLogDto(l.LogId, l.UserId, l.User.FullName, l.User.Email, l.FacilityId, l.Action,
                l.EntityName, l.EntityId, l.IpAddress, l.Result, l.CreatedAt))
            .ToPagedResultAsync(page, pageSize, ct);
    }

    private static string? Truncate(string? value, int max) => value is { Length: > 0 } && value.Length > max ? value[..max] : value;
}
