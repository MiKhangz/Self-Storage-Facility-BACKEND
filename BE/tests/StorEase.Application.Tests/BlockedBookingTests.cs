using FluentAssertions;
using StorEase.Application.Common;
using StorEase.Application.Features.Maintenance;
using StorEase.Application.Features.Maintenance.Dtos;
using StorEase.Application.Features.Reservations;
using StorEase.Application.Features.Reservations.Dtos;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

/// <summary>A customer never loses the deposit, or is called a no-show, because StorEase had no unit for them.</summary>
public class BlockedBookingTests
{
    private static readonly DateOnly Today = VietnamTime.Today;

    private static ReservationService As(TestDb t, Application.Interfaces.ICurrentUser user) => new(t.Uow, user, new FakeEmailService());

    /// <summary>A confirmed booking of A-101 whose deposit is paid, long after the free-cancellation window.</summary>
    private static int Booked(TestDb t, DateOnly moveIn, DateTime? slot = null)
    {
        var booking = new Reservation
        {
            CustomerId = CustomerId, FacilityId = FacilityId, UnitTypeId = SmallTypeId, StorageUnitId = A101,
            ReservationCode = "RS-NEXT", MoveInDate = moveIn, RentalMonths = 3, HandoverSlot = slot,
            Status = ReservationStatus.Confirmed, CreatedAt = DateTime.UtcNow.AddDays(-30),
        };
        t.Db.Add(booking);
        t.Db.SaveChanges();
        t.Db.Add(new Invoice
        {
            CustomerId = CustomerId, ReservationId = booking.ReservationId, InvoiceNumber = "DEP-NEXT", IssueDate = Today, DueDate = Today,
            TotalAmount = 900_000m, PaidAmount = 900_000m, Status = InvoiceStatus.Paid,
        });
        Set(t, A101, StorageUnitStatus.Reserved);
        return booking.ReservationId;
    }

    private static void Set(TestDb t, int unitId, StorageUnitStatus status)
    {
        t.Db.StorageUnits.Find(unitId)!.Status = status;
        t.Db.SaveChanges();
    }

    private static Contract Renter(TestDb t, DateOnly end, ContractStatus status)
    {
        var booking = new Reservation
        {
            CustomerId = OtherCustomerId, FacilityId = FacilityId, UnitTypeId = SmallTypeId, StorageUnitId = A101,
            ReservationCode = "RS-PREV", MoveInDate = end.AddDays(-30), RentalMonths = 1, Status = ReservationStatus.HandedOver, CreatedAt = DateTime.UtcNow,
        };
        t.Db.Add(booking);
        t.Db.SaveChanges();
        var contract = new Contract
        {
            ReservationId = booking.ReservationId, CustomerId = OtherCustomerId, StorageUnitId = A101, ContractNumber = "CT-PREV",
            StartDate = end.AddDays(-30), EndDate = end, MonthlyRent = 1, DepositAmount = 1, PaymentDay = 1, Status = status,
        };
        t.Db.Add(contract);
        Set(t, A101, StorageUnitStatus.Occupied);
        return contract;
    }

    private static async Task<Reservation> RunJob(TestDb t, int id, DateOnly today)
    {
        t.Db.ChangeTracker.Clear();
        await As(t, Staff).RunDailyAsync(today, 10, default);
        t.Db.ChangeTracker.Clear();
        return t.Db.Reservations.Find(id)!;
    }

    private static void ShouldBeRefunded(TestDb t, Reservation booking)
    {
        booking.Status.Should().Be(ReservationStatus.Cancelled, "this is not the customer's no-show");
        t.Db.DepositRefunds.Should().ContainSingle(r => r.ReservationId == booking.ReservationId).Which.Amount.Should().Be(900_000m);
    }

    [Fact]
    public async Task With_no_unit_free_the_booking_is_flagged_then_cancelled_with_the_whole_deposit()
    {
        using var t = Create();
        var id = Booked(t, Today.AddDays(2));
        Renter(t, Today.AddDays(-5), ContractStatus.Overstay);
        Set(t, A102, StorageUnitStatus.Maintenance);

        var before = await RunJob(t, id, Today);
        var after = await RunJob(t, id, Today.AddDays(3));

        before.UnitBlocked.Should().BeTrue("staff must see it and call the customer");
        before.ProposedStorageUnitId.Should().BeNull();
        ShouldBeRefunded(t, after);
    }

    [Fact]
    public async Task An_offer_left_unanswered_is_not_a_no_show()
    {
        using var t = Create();
        var id = Booked(t, Today.AddDays(2));
        t.Db.Reservations.Find(id)!.ProposedStorageUnitId = A102;
        t.Db.SaveChanges();

        ShouldBeRefunded(t, await RunJob(t, id, Today.AddDays(3)));
    }

    [Fact]
    public async Task A_held_unit_under_repair_is_offered_a_replacement()
    {
        using var t = Create();
        var id = Booked(t, Today.AddDays(2));
        Set(t, A101, StorageUnitStatus.Maintenance);

        (await RunJob(t, id, Today)).ProposedStorageUnitId.Should().Be(A102);
    }

    [Fact]
    public async Task A_unit_freed_late_on_the_handover_day_still_does_not_forfeit()
    {
        using var t = Create();
        var id = Booked(t, Today);
        var renter = Renter(t, Today.AddDays(-1), ContractStatus.Overstay);
        Set(t, A102, StorageUnitStatus.Maintenance);
        await RunJob(t, id, Today);

        t.Db.Contracts.Find(renter.ContractId)!.Status = ContractStatus.Closed;
        Set(t, A101, StorageUnitStatus.Reserved);
        await RunJob(t, id, Today);

        ShouldBeRefunded(t, await RunJob(t, id, Today.AddDays(1)));
    }

    [Fact]
    public async Task A_block_that_clears_before_the_handover_day_leaves_the_normal_no_show_rule()
    {
        using var t = Create();
        var id = Booked(t, Today.AddDays(5));
        var renter = Renter(t, Today.AddDays(-1), ContractStatus.Overstay);
        Set(t, A102, StorageUnitStatus.Maintenance);
        await RunJob(t, id, Today);

        t.Db.Contracts.Find(renter.ContractId)!.Status = ContractStatus.Closed;
        Set(t, A101, StorageUnitStatus.Reserved);
        (await RunJob(t, id, Today)).UnitBlocked.Should().BeFalse();

        (await RunJob(t, id, Today.AddDays(6))).Status.Should().Be(ReservationStatus.Expired);
        t.Db.DepositRefunds.Should().BeEmpty();
    }

    [Fact]
    public async Task An_early_appointment_on_a_still_rented_unit_is_cleared_and_the_booking_kept()
    {
        using var t = Create();
        var id = Booked(t, Today.AddDays(8), VietnamTime.ToUtc(Today.AddDays(-1).ToDateTime(new TimeOnly(9, 0))));
        Renter(t, Today.AddDays(6), ContractStatus.Active);

        var after = await RunJob(t, id, Today);

        after.Status.Should().Be(ReservationStatus.Confirmed);
        after.HandoverSlot.Should().BeNull("move-in day counts again");
    }

    [Fact]
    public async Task Staff_cannot_mark_a_blocked_booking_as_a_no_show()
    {
        using var t = Create();
        var id = Booked(t, Today.AddDays(-1));
        Renter(t, Today.AddDays(-5), ContractStatus.Overstay);
        t.Db.ChangeTracker.Clear();

        var result = await As(t, Staff).MarkNoShowAsync(id, default);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task A_customer_cancelling_a_blocked_booking_gets_the_deposit_back_after_the_free_window()
    {
        using var t = Create();
        var id = Booked(t, Today.AddDays(3));
        Renter(t, Today.AddDays(-5), ContractStatus.Overstay);
        t.Db.ChangeTracker.Clear();

        var result = await As(t, Customer).CancelAsync(id, new CancelReservationRequest(null), default);

        result.Value!.DepositRefundable.Should().BeTrue();
    }

    [Fact]
    public async Task An_offer_that_stopped_being_usable_is_withdrawn()
    {
        using var t = Create();
        var id = Booked(t, Today.AddDays(2));
        t.Db.Reservations.Find(id)!.ProposedStorageUnitId = A102;
        Set(t, A102, StorageUnitStatus.Maintenance);

        (await RunJob(t, id, Today)).ProposedStorageUnitId.Should().BeNull();
    }

    [Fact]
    public async Task Accepting_a_unit_that_went_under_repair_is_refused()
    {
        using var t = Create();
        var id = Booked(t, Today.AddDays(2));
        t.Db.Reservations.Find(id)!.ProposedStorageUnitId = A102;
        Set(t, A102, StorageUnitStatus.Maintenance);
        t.Db.ChangeTracker.Clear();

        (await As(t, Customer).AcceptProposedUnitAsync(id, default)).Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Changing_the_unit_at_the_desk_withdraws_the_open_offer()
    {
        using var t = Create();
        var id = Booked(t, Today.AddDays(2));
        t.Db.Reservations.Find(id)!.ProposedStorageUnitId = A102;
        t.Db.SaveChanges();
        t.Db.ChangeTracker.Clear();

        (await As(t, Staff).ChangeUnitAsync(id, new AssignUnitRequest(A102), default)).IsSuccess.Should().BeTrue();

        t.Db.ChangeTracker.Clear();
        t.Db.Reservations.Find(id)!.ProposedStorageUnitId.Should().BeNull();
    }

    [Fact]
    public async Task A_repaired_unit_goes_back_to_the_booking_that_holds_it()
    {
        using var t = Create();
        Booked(t, Today.AddDays(2));
        Set(t, A101, StorageUnitStatus.Maintenance);
        var order = new MaintenanceOrder { StorageUnitId = A101, Description = "Fix", Status = MaintenanceOrderStatus.InProgress };
        t.Db.Add(order);
        t.Db.SaveChanges();
        t.Db.ChangeTracker.Clear();

        var done = await new MaintenanceService(t.Uow, Staff).ChangeStatusAsync(
            order.MaintenanceOrderId, new ChangeMaintenanceStatusRequest(MaintenanceOrderStatus.Done, null, 0m, "Fixed"), default);

        done.IsSuccess.Should().BeTrue(done.Error?.Message);
        t.Db.ChangeTracker.Clear();
        t.Db.StorageUnits.Find(A101)!.Status.Should().Be(StorageUnitStatus.Reserved, "the handover needs a Reserved unit");
    }
}
