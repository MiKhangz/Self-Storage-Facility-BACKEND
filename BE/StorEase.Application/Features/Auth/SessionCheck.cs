using Microsoft.EntityFrameworkCore;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Auth;

/// <summary>
/// A login token carries the role it was issued with and lasts hours. Each request checks the account still exists, is
/// Active and has that role, so disabling or demoting someone takes effect at once instead of when the token expires.
/// </summary>
public static class SessionCheck
{
    public static async Task<bool> StillValidAsync(IUnitOfWork uow, string? sub, string? role, CancellationToken ct)
    {
        if (!int.TryParse(sub, out var userId))
            return false;
        var user = await uow.Repository<User>().Query().AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => new { u.Status, u.Role.RoleName })
            .FirstOrDefaultAsync(ct);
        return user is not null && user.Status == UserStatus.Active && user.RoleName == role;
    }
}
