using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.ActivityLogs;

public sealed record ActivityEntry(
    int UserId, int? FacilityId, string Action, string? EntityName, string? EntityId, string? IpAddress, bool Succeeded);

public sealed record ActivityLogDto(
    long LogId, int UserId, string UserName, string UserEmail, string UserRole, int? FacilityId, string Action,
    string? EntityName, string? EntityId, string? IpAddress, ActivityResult Result, DateTime CreatedAt,
    int? SubjectUserId = null, string? Subject = null);

public interface IActivityLogService
{
    /// <summary>Saves one audit row. Called by the API's activity filter after every write, and by login.</summary>
    Task RecordAsync(ActivityEntry entry, CancellationToken ct);

    /// <param name="search">Part of the user's name or email.</param>
    /// <param name="signIns">False leaves out sign-in attempts.</param>
    Task<Result<PagedResult<ActivityLogDto>>> ListAsync(
        int? userId, string? role, string? search, string? action, bool signIns, DateOnly? from, DateOnly? to,
        int page, int pageSize, CancellationToken ct);
}

public class ActivityLogService(IUnitOfWork uow) : IActivityLogService
{
    /// <summary>The action AuthService records for every sign-in attempt.</summary>
    public const string SignIn = "LOGIN";

    /// <summary>The controller name the activity filter records for check-ins and check-outs.</summary>
    private const string Visits = "CheckIns";

    /// <summary>The duplicate the activity filter wrote for a sign-in made while already signed in, before it stopped.</summary>
    private const string LegacySignIn = "POST Auth.Login";

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
        int? userId, string? role, string? search, string? action, bool signIns, DateOnly? from, DateOnly? to,
        int page, int pageSize, CancellationToken ct)
    {
        var query = uow.Repository<ActivityLog>().Query();
        var visits = uow.Repository<CheckIn>().Query();
        if (userId is not null)
            query = query.Where(l => l.UserId == userId
                || (l.EntityName == Visits && visits.Any(v => v.CheckInId.ToString() == l.EntityId && v.Contract.CustomerId == userId)));
        if (!string.IsNullOrWhiteSpace(role))
            query = query.Where(l => l.User.Role.RoleName == role);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(l => l.User.FullName.Contains(term) || l.User.Email.Contains(term));
        }
        if (!signIns)
            query = query.Where(l => l.Action != SignIn && l.Action != LegacySignIn);
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
            .Select(l => new ActivityLogDto(l.LogId, l.UserId, l.User.FullName, l.User.Email, l.User.Role.RoleName, l.FacilityId, l.Action,
                l.EntityName, l.EntityId, l.IpAddress, l.Result, l.CreatedAt,
                l.EntityName == Visits ? visits.Where(v => v.CheckInId.ToString() == l.EntityId).Select(v => (int?)v.Contract.CustomerId).FirstOrDefault() : null,
                l.EntityName == Visits ? visits.Where(v => v.CheckInId.ToString() == l.EntityId)
                    .Select(v => v.Contract.Customer.FullName + " · " + v.StorageUnit.UnitCode).FirstOrDefault() : null))
            .ToPagedResultAsync(page, pageSize, ct);
    }

    private static string? Truncate(string? value, int max) => value is { Length: > 0 } && value.Length > max ? value[..max] : value;
}
