using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using StorEase.Application.Common;
using StorEase.Application.Features.Auth;
using StorEase.Application.Features.Users;
using StorEase.Domain.Enums;
using StorEase.Infrastructure.Services;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

/// <summary>A token stops working as soon as its account is disabled or its role changes.</summary>
public class SessionCheckTests
{
    [Fact]
    public async Task A_token_is_good_only_while_the_account_is_active_with_the_same_role()
    {
        using var t = Create();

        var stamp = SessionCheck.StampOf("x");
        var fine = await SessionCheck.StillValidAsync(t.Uow, StaffId.ToString(), Roles.FacilityStaff, stamp, default);
        var otherRole = await SessionCheck.StillValidAsync(t.Uow, StaffId.ToString(), Roles.SystemAdministrator, stamp, default);
        var unknown = await SessionCheck.StillValidAsync(t.Uow, "9999", Roles.FacilityStaff, stamp, default);
        var noStamp = await SessionCheck.StillValidAsync(t.Uow, StaffId.ToString(), Roles.FacilityStaff, null, default);
        noStamp.Should().BeFalse("a token from before sessions followed the password must sign in again");
        t.Db.Users.Find(StaffId)!.Status = UserStatus.Disabled;
        t.Db.SaveChanges();
        var disabled = await SessionCheck.StillValidAsync(t.Uow, StaffId.ToString(), Roles.FacilityStaff, stamp, default);

        fine.Should().BeTrue();
        otherRole.Should().BeFalse("a demoted or promoted user must sign in again");
        unknown.Should().BeFalse();
        disabled.Should().BeFalse("a disabled account loses access on its next request");
    }

    [Fact]
    public async Task A_password_change_ends_every_session_but_the_one_that_made_it()
    {
        using var t = Create();
        var hasher = new Pbkdf2PasswordHasher();
        var hash = hasher.Hash("Old@12345");
        t.Db.Users.Find(StaffId)!.PasswordHash = hash;
        t.Db.SaveChanges();
        t.Db.ChangeTracker.Clear();
        var jwt = new JwtService(new JwtOptions { Issuer = "StorEase", Audience = "StorEase.Frontend", Key = new string('k', 64), ExpiresMinutes = 60 });
        var before = SessionCheck.StampOf(hash);

        var token = await new ProfileService(t.Uow, Staff, hasher, jwt).ChangePasswordAsync(new ChangePasswordRequest("Old@12345", "New@12345"), default);

        t.Db.ChangeTracker.Clear();
        (await SessionCheck.StillValidAsync(t.Uow, StaffId.ToString(), Roles.FacilityStaff, before, default)).Should().BeFalse();
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(token.Value).Claims.ToDictionary(c => c.Type, c => c.Value);
        (await SessionCheck.StillValidAsync(t.Uow, claims["sub"], claims["role"], claims[SessionCheck.StampClaim], default)).Should().BeTrue();
    }
}
