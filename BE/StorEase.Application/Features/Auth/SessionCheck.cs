using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Auth;

/// <summary>
/// A login token carries the role and password stamp it was issued with and lasts hours. Each request checks the
/// account still exists, is Active and has that role and password, so disabling or demoting someone, or a new
/// password, takes effect at once instead of when the token expires.
/// </summary>
public static class SessionCheck
{
    public const string StampClaim = "pwd";

    /// <summary>A short fingerprint of the stored hash: it changes whenever the password does.</summary>
    public static string StampOf(string passwordHash) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(passwordHash)))[..16];

    public static async Task<bool> StillValidAsync(IUnitOfWork uow, string? sub, string? role, string? stamp, CancellationToken ct)
    {
        if (!int.TryParse(sub, out var userId))
            return false;
        var user = await uow.Repository<User>().Query().AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => new { u.Status, u.Role.RoleName, u.PasswordHash })
            .FirstOrDefaultAsync(ct);
        return user is not null && user.Status == UserStatus.Active && user.RoleName == role && StampOf(user.PasswordHash) == stamp;
    }
}
