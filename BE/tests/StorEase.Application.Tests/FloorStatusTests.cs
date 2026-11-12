using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Facilities;
using StorEase.Application.Features.Facilities.Dtos;
using StorEase.Domain.Enums;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

/// <summary>A floor can close for construction or renovation, and is removed only when no customer holds a unit on it.</summary>
public class FloorStatusTests
{
    private static FloorService Admin(TestDb t) => new(t.Uow, new FakeCurrentUser(99, Roles.SystemAdministrator));

    private static async Task<StorageUnitStatus> StatusOf(TestDb t, int unitId)
    {
        t.Db.ChangeTracker.Clear();
        return (await t.Db.StorageUnits.SingleAsync(u => u.StorageUnitId == unitId)).Status;
    }

    [Fact]
    public async Task Closing_a_floor_takes_its_free_units_off_sale_and_reopening_puts_them_back()
    {
        using var t = Create();
        SeedReadyForHandover(t, signed: true); // A-101 Reserved

        var closed = await Admin(t).UpdateAsync(1, new FloorRequest(1, "Tầng 1", FloorStatus.Renovation), default);
        var a102Closed = await StatusOf(t, A102);
        var a101Closed = await StatusOf(t, A101);
        var reopened = await Admin(t).UpdateAsync(1, new FloorRequest(1, "Tầng 1", FloorStatus.Open), default);

        closed.Value!.Status.Should().Be(FloorStatus.Renovation);
        closed.Value.RentedUnits.Should().Be(1);
        a102Closed.Should().Be(StorageUnitStatus.Maintenance);
        a101Closed.Should().Be(StorageUnitStatus.Reserved, "a customer's unit keeps its booking");
        reopened.IsSuccess.Should().BeTrue();
        (await StatusOf(t, A102)).Should().Be(StorageUnitStatus.Available);
    }

    [Fact]
    public async Task A_floor_with_a_customer_on_it_cannot_be_removed()
    {
        using var t = Create();
        SeedReadyForHandover(t, signed: true);

        var refused = await Admin(t).DeleteAsync(1, default);

        refused.Error!.Type.Should().Be(ErrorType.Conflict);
        refused.Error.Message.Should().Contain("A-101");
    }

    [Fact]
    public async Task An_unused_floor_is_deleted_and_one_with_history_is_retired_and_hidden()
    {
        using var t = Create();
        var fresh = await Admin(t).CreateAsync(FacilityId, new FloorRequest(2, null), default);
        var history = await Admin(t).CreateAsync(FacilityId, new FloorRequest(3, null), default);
        // A past booking on floor 3's only unit: it is history, not a current customer.
        t.Db.Add(new Domain.Entities.StorageUnit
        {
            StorageUnitId = 50, FacilityId = FacilityId, FloorId = history.Value!.FloorId, UnitTypeId = SmallTypeId, UnitCode = "C-301",
            Status = StorageUnitStatus.Available, CurrentPrice = 1m,
        });
        t.Db.Add(new Domain.Entities.Reservation
        {
            CustomerId = CustomerId, FacilityId = FacilityId, UnitTypeId = SmallTypeId, StorageUnitId = 50, ReservationCode = "RS-OLD",
            MoveInDate = MoveIn, RentalMonths = 1, Status = ReservationStatus.Cancelled, CreatedAt = DateTime.UtcNow,
        });
        t.Db.SaveChanges();

        (await Admin(t).DeleteAsync(fresh.Value!.FloorId, default)).IsSuccess.Should().BeTrue();
        (await Admin(t).DeleteAsync(history.Value.FloorId, default)).IsSuccess.Should().BeTrue();
        var reused = await Admin(t).CreateAsync(FacilityId, new FloorRequest(3, "New 3"), default);

        t.Db.ChangeTracker.Clear();
        (await t.Db.Floors.AnyAsync(f => f.FloorId == fresh.Value.FloorId)).Should().BeFalse();
        (await StatusOf(t, 50)).Should().Be(StorageUnitStatus.Retired);
        (await Admin(t).ListAsync(FacilityId, default)).Value!.Select(f => f.FloorNumber).Should().Equal(1, 3);
        reused.IsSuccess.Should().BeTrue("a removed floor gives its number back");
    }
}
