using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Domain.Entities;

namespace StorEase.Application.Tests;

public class StaffScopeTests
{
    [Fact]
    public async Task FacilityIdsOf_returns_assigned_and_managed_facilities_only()
    {
        using var t = new TestDb();
        var role = new Role { RoleName = Roles.FacilityManager };
        var user = NewUser(role, "a@x.vn");
        var other = NewUser(role, "b@x.vn");
        var f1 = NewFacility("F1");
        var f2 = NewFacility("F2");
        var f3 = NewFacility("F3");
        f2.Manager = user;
        t.Db.AddRange(f1, f2, f3, other);
        t.Db.StaffFacilities.AddRange(
            new StaffFacility { User = user, Facility = f1, AssignedDate = new DateOnly(2026, 10, 1) },
            new StaffFacility { User = other, Facility = f3, AssignedDate = new DateOnly(2026, 10, 1) });
        await t.Db.SaveChangesAsync();

        var ids = await StaffScope.FacilityIdsOf(t.Uow, user.UserId).ToListAsync();

        ids.Should().BeEquivalentTo([f1.FacilityId, f2.FacilityId]);
    }

    [Fact]
    public async Task FacilityIdsOf_does_not_repeat_a_facility_that_is_both_assigned_and_managed()
    {
        using var t = new TestDb();
        var user = NewUser(new Role { RoleName = Roles.FacilityManager }, "a@x.vn");
        var f1 = NewFacility("F1");
        f1.Manager = user;
        t.Db.StaffFacilities.Add(new StaffFacility { User = user, Facility = f1, AssignedDate = new DateOnly(2026, 10, 1) });
        await t.Db.SaveChangesAsync();

        var ids = await StaffScope.FacilityIdsOf(t.Uow, user.UserId).ToListAsync();

        ids.Should().ContainSingle().Which.Should().Be(f1.FacilityId);
    }

    [Theory]
    [InlineData(Roles.SystemAdministrator, true)]
    [InlineData(Roles.BusinessOperationsManager, true)]
    [InlineData(Roles.FacilityManager, false)]
    [InlineData(Roles.FacilityStaff, false)]
    public void SeesAllFacilities_depends_on_role(string role, bool expected) =>
        StaffScope.SeesAllFacilities(role).Should().Be(expected);

    [Fact]
    public async Task StorageUnit_and_Facility_lists_only_return_assigned_facilities_for_manager_and_staff()
    {
        using var t = CoreFlowSeed.Create();
        var f2 = NewFacility("F2");
        var floor2 = new Floor { Facility = f2, FloorNumber = 1, Name = "Floor 1", Status = StorEase.Domain.Enums.FloorStatus.Open };
        var u2 = new StorageUnit
        {
            Facility = f2, Floor = floor2, UnitTypeId = CoreFlowSeed.SmallTypeId,
            UnitCode = "F2-101", Status = StorEase.Domain.Enums.StorageUnitStatus.Available, CurrentPrice = 900_000m,
        };
        t.Db.AddRange(f2, floor2, u2);
        await t.Db.SaveChangesAsync();

        var manager = new FakeCurrentUser(CoreFlowSeed.StaffId, Roles.FacilityManager);
        var managerUnits = await new StorEase.Application.Features.StorageUnits.StorageUnitService(t.Uow, manager)
            .ListAsync(null, null, null, null, 1, 50, default);
        var managerOtherUnit = await new StorEase.Application.Features.StorageUnits.StorageUnitService(t.Uow, manager)
            .GetAsync(u2.StorageUnitId, default);
        var managerFacilities = await new StorEase.Application.Features.Facilities.FacilityService(t.Uow, manager)
            .ListAsync(null, null, false, 1, 50, default);
        var adminUnits = await new StorEase.Application.Features.StorageUnits.StorageUnitService(
                t.Uow, new FakeCurrentUser(99, Roles.SystemAdministrator))
            .ListAsync(null, null, null, null, 1, 50, default);

        managerUnits.Value!.Items.Should().OnlyContain(u => u.FacilityId == CoreFlowSeed.FacilityId);
        managerOtherUnit.Error!.Type.Should().Be(ErrorType.NotFound);
        managerFacilities.Value!.Items.Select(f => f.FacilityId).Should().Equal(CoreFlowSeed.FacilityId);
        adminUnits.Value!.Items.Select(u => u.FacilityId).Should().Contain([CoreFlowSeed.FacilityId, f2.FacilityId]);
    }

    private static User NewUser(Role role, string email) => new()
    {
        Role = role, FullName = "Test", Email = email, PasswordHash = "x", CreatedAt = DateTime.UtcNow,
    };

    private static Facility NewFacility(string code) => new()
    {
        Code = code, Name = code, Address = "addr", District = "d",
    };
}
