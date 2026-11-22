using FluentAssertions;
using StorEase.Application.Common;
using StorEase.Application.Features.StorageUnits;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

public class UnitNoteTests
{
    [Theory]
    [InlineData("", false)] // signed out
    [InlineData(Roles.StorageCustomer, false)]
    [InlineData(Roles.FacilityStaff, true)]
    [InlineData(Roles.FacilityManager, true)]
    [InlineData(Roles.BusinessOperationsManager, true)]
    [InlineData(Roles.SystemAdministrator, true)]
    public async Task Only_staff_read_a_units_note(string role, bool reads)
    {
        using var t = Create();
        t.Db.StorageUnits.Find(A101)!.Note = "Door sticks";
        t.Db.SaveChanges();
        var units = new StorageUnitService(t.Uow, new FakeCurrentUser(0, role));

        var one = await units.GetAsync(A101, default);
        var list = await units.ListAsync(null, null, null, null, 1, 20, default);

        var expected = reads ? "Door sticks" : null;
        one.Value!.Note.Should().Be(expected);
        list.Value!.Items.Single(u => u.StorageUnitId == A101).Note.Should().Be(expected);
    }
}
