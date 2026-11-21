using FluentAssertions;
using StorEase.Application.Common;
using StorEase.Application.Features.Auth;
using StorEase.Domain.Enums;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

/// <summary>A token stops working as soon as its account is disabled or its role changes.</summary>
public class SessionCheckTests
{
    [Fact]
    public async Task A_token_is_good_only_while_the_account_is_active_with_the_same_role()
    {
        using var t = Create();

        var fine = await SessionCheck.StillValidAsync(t.Uow, StaffId.ToString(), Roles.FacilityStaff, default);
        var otherRole = await SessionCheck.StillValidAsync(t.Uow, StaffId.ToString(), Roles.SystemAdministrator, default);
        var unknown = await SessionCheck.StillValidAsync(t.Uow, "9999", Roles.FacilityStaff, default);
        t.Db.Users.Find(StaffId)!.Status = UserStatus.Disabled;
        t.Db.SaveChanges();
        var disabled = await SessionCheck.StillValidAsync(t.Uow, StaffId.ToString(), Roles.FacilityStaff, default);

        fine.Should().BeTrue();
        otherRole.Should().BeFalse("a demoted or promoted user must sign in again");
        unknown.Should().BeFalse();
        disabled.Should().BeFalse("a disabled account loses access on its next request");
    }
}
