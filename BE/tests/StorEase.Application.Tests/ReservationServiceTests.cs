using FluentAssertions;
using StorEase.Application.Common;
using StorEase.Application.Features.Reservations;
using StorEase.Application.Features.Reservations.Dtos;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

public class ReservationServiceTests
{
    private static ReservationService As(TestDb t, Application.Interfaces.ICurrentUser user) => new(t.Uow, user, new FakeEmailService());

    private static async Task<int> Reserve(TestDb t, int unitTypeId = SmallTypeId)
    {
        var result = await As(t, Customer).CreateAsync(new CreateReservationRequest(FacilityId, unitTypeId, MoveIn, 3), default);
        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        return result.Value!.ReservationId;
    }

    private static StorageUnitStatus UnitStatus(TestDb t, int unitId)
    {
        t.Db.ChangeTracker.Clear();
        return t.Db.StorageUnits.Find(unitId)!.Status;
    }

    [Fact]
    public async Task Reserving_creates_a_pending_reservation_and_holds_no_unit()
    {
        using var t = Create();

        var result = await As(t, Customer).CreateAsync(new CreateReservationRequest(FacilityId, SmallTypeId, MoveIn, 3), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(ReservationStatus.Pending);
        result.Value.StorageUnitId.Should().BeNull();
        result.Value.CustomerId.Should().Be(CustomerId);
        result.Value.ReservationCode.Should().StartWith("RS");
        t.Db.StorageUnits.Should().OnlyContain(u => u.Status == StorageUnitStatus.Available, "business rule 2: reserving does not occupy a unit");
    }

    [Fact]
    public async Task Reserving_a_type_with_no_available_unit_is_a_conflict()
    {
        using var t = Create();
        t.Db.StorageUnits.Find(A104)!.Status = StorageUnitStatus.Occupied;
        t.Db.SaveChanges();

        var result = await As(t, Customer).CreateAsync(new CreateReservationRequest(FacilityId, LargeTypeId, MoveIn, 3), default);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
        t.Db.Reservations.Should().BeEmpty();
    }

    [Fact]
    public async Task Reserving_at_an_unknown_facility_is_not_found()
    {
        using var t = Create();

        var result = await As(t, Customer).CreateAsync(new CreateReservationRequest(99, SmallTypeId, MoveIn, 3), default);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Assigning_a_unit_confirms_the_reservation_and_reserves_only_that_unit()
    {
        using var t = Create();
        var id = await Reserve(t);

        var result = await As(t, Staff).AssignUnitAsync(id, new AssignUnitRequest(A101), default);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value!.Status.Should().Be(ReservationStatus.Confirmed);
        result.Value.UnitCode.Should().Be("A-101");
        UnitStatus(t, A101).Should().Be(StorageUnitStatus.Reserved);
        UnitStatus(t, A102).Should().Be(StorageUnitStatus.Available);
        UnitStatus(t, A104).Should().Be(StorageUnitStatus.Available);
    }

    [Fact]
    public async Task A_unit_of_another_type_cannot_be_assigned()
    {
        using var t = Create();
        var id = await Reserve(t, SmallTypeId);

        var result = await As(t, Staff).AssignUnitAsync(id, new AssignUnitRequest(A104), default);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Message.Should().Contain("unit type");
    }

    [Fact]
    public async Task A_unit_that_is_not_available_cannot_be_assigned()
    {
        using var t = Create();
        var id = await Reserve(t);
        t.Db.StorageUnits.Find(A101)!.Status = StorageUnitStatus.Maintenance;
        t.Db.SaveChanges();

        var result = await As(t, Staff).AssignUnitAsync(id, new AssignUnitRequest(A101), default);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Message.Should().Contain("not Available");
    }

    [Fact]
    public async Task A_unit_with_a_draft_contract_cannot_be_assigned_again()
    {
        using var t = Create();
        // An earlier reservation already holds a Draft contract on A-101.
        var earlier = new Reservation
        {
            CustomerId = OtherCustomerId, FacilityId = FacilityId, UnitTypeId = SmallTypeId, StorageUnitId = A101,
            ReservationCode = "RS-OLD", MoveInDate = MoveIn, RentalMonths = 1, Status = ReservationStatus.Confirmed, CreatedAt = DateTime.UtcNow,
        };
        t.Db.Add(earlier);
        t.Db.SaveChanges();
        t.Db.Add(new Contract
        {
            ReservationId = earlier.ReservationId, CustomerId = OtherCustomerId, StorageUnitId = A101, ContractNumber = "CT-OLD",
            StartDate = MoveIn, EndDate = MoveIn.AddMonths(1), MonthlyRent = 1, DepositAmount = 1, PaymentDay = 1, Status = ContractStatus.Draft,
        });
        t.Db.SaveChanges();
        var id = await Reserve(t);

        var result = await As(t, Staff).AssignUnitAsync(id, new AssignUnitRequest(A101), default);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Message.Should().Contain("Active or Draft contract", "business rule 1");
    }

    [Fact]
    public async Task Only_a_pending_reservation_can_be_assigned()
    {
        using var t = Create();
        var id = await Reserve(t);
        await As(t, Staff).AssignUnitAsync(id, new AssignUnitRequest(A101), default);

        var again = await As(t, Staff).AssignUnitAsync(id, new AssignUnitRequest(A102), default);

        again.Error!.Type.Should().Be(ErrorType.Conflict);
        UnitStatus(t, A102).Should().Be(StorageUnitStatus.Available);
    }

    [Fact]
    public async Task A_refused_assignment_changes_nothing()
    {
        using var t = Create();
        var id = await Reserve(t);

        await As(t, Staff).AssignUnitAsync(id, new AssignUnitRequest(A104), default);

        t.Db.ChangeTracker.Clear();
        var reservation = t.Db.Reservations.Find(id)!;
        reservation.Status.Should().Be(ReservationStatus.Pending);
        reservation.StorageUnitId.Should().BeNull();
        UnitStatus(t, A104).Should().Be(StorageUnitStatus.Available);
    }

    [Fact]
    public async Task A_customer_cannot_see_another_customers_reservation()
    {
        using var t = Create();
        var id = await Reserve(t);

        var single = await As(t, OtherCustomer).GetAsync(id, default);
        var list = await As(t, OtherCustomer).ListAsync(null, null, null, null, null, 1, 20, default);

        single.Error!.Type.Should().Be(ErrorType.NotFound, "404, not 403, so ids cannot be probed");
        list.Value!.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Staff_see_every_reservation_and_can_filter_by_status()
    {
        using var t = Create();
        var first = await Reserve(t);
        await Reserve(t);
        await As(t, Staff).AssignUnitAsync(first, new AssignUnitRequest(A101), default);

        var all = await As(t, Staff).ListAsync(null, null, null, null, null, 1, 20, default);
        var pending = await As(t, Staff).ListAsync(ReservationStatus.Pending, null, null, null, null, 1, 20, default);

        all.Value!.TotalCount.Should().Be(2);
        pending.Value!.Items.Should().ContainSingle().Which.Status.Should().Be(ReservationStatus.Pending);
    }
}
