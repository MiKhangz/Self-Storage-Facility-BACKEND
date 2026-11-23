using FluentAssertions;
using StorEase.Application.Common;
using StorEase.Application.Features.AccessCodes;
using StorEase.Application.Features.Contracts;
using StorEase.Application.Features.Handovers;
using StorEase.Application.Features.Handovers.Dtos;
using StorEase.Application.Features.Reservations;
using StorEase.Application.Features.Reservations.Dtos;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using StorEase.Infrastructure.Services;
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

    /// <summary>Another customer's contract holds the unit for these dates.</summary>
    private static void RentOut(TestDb t, int unitId, DateOnly start, DateOnly end, ContractStatus status = ContractStatus.Active)
    {
        var booking = new Reservation
        {
            CustomerId = OtherCustomerId, FacilityId = FacilityId, UnitTypeId = t.Db.StorageUnits.Find(unitId)!.UnitTypeId, StorageUnitId = unitId,
            ReservationCode = $"RS-OUT{unitId}{start.DayNumber}", MoveInDate = start, RentalMonths = 1, Status = ReservationStatus.HandedOver, CreatedAt = DateTime.UtcNow,
        };
        t.Db.Add(booking);
        t.Db.SaveChanges();
        t.Db.Add(new Contract
        {
            ReservationId = booking.ReservationId, CustomerId = OtherCustomerId, StorageUnitId = unitId, ContractNumber = $"CT-OUT{unitId}{start.DayNumber}",
            StartDate = start, EndDate = end, MonthlyRent = 1, DepositAmount = 1, PaymentDay = 1, Status = status,
        });
        t.Db.StorageUnits.Find(unitId)!.Status = StorageUnitStatus.Occupied;
        t.Db.SaveChanges();
    }

    private static StorageUnitStatus UnitStatus(TestDb t, int unitId)
    {
        t.Db.ChangeTracker.Clear();
        return t.Db.StorageUnits.Find(unitId)!.Status;
    }

    [Fact]
    public async Task Reserving_creates_a_pending_reservation_holding_the_expected_unit()
    {
        using var t = Create();

        var result = await As(t, Customer).CreateAsync(new CreateReservationRequest(FacilityId, SmallTypeId, MoveIn, 3), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(ReservationStatus.Pending);
        result.Value.UnitCode.Should().Be("A-101", "the first unit free for the whole stay is held");
        result.Value.CustomerId.Should().Be(CustomerId);
        result.Value.ReservationCode.Should().StartWith("RS");
        UnitStatus(t, A101).Should().Be(StorageUnitStatus.Reserved);
        UnitStatus(t, A102).Should().Be(StorageUnitStatus.Available);
    }

    [Fact]
    public async Task Reserving_a_type_with_no_available_unit_is_a_conflict()
    {
        using var t = Create();
        // The only large unit is rented through the whole stay.
        RentOut(t, A104, MoveIn.AddDays(-30), MoveIn.AddMonths(6));

        var result = await As(t, Customer).CreateAsync(new CreateReservationRequest(FacilityId, LargeTypeId, MoveIn, 3), default);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
        t.Db.Reservations.Should().NotContain(r => r.CustomerId == CustomerId);
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
        result.Error.Message.Should().Contain("Maintenance");
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
        result.Error.Message.Should().Contain("booked or rented", "business rule 1: a Draft contract already claims its unit");
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
        reservation.StorageUnitId.Should().Be(A101, "the expected unit stays held");
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

    [Fact]
    public async Task A_unit_rented_now_can_be_booked_when_the_renter_leaves_before_move_in()
    {
        using var t = Create();
        RentOut(t, A104, MoveIn.AddDays(-60), MoveIn.AddDays(-1));

        var result = await As(t, Customer).CreateAsync(new CreateReservationRequest(FacilityId, LargeTypeId, MoveIn, 3), default);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value!.UnitCode.Should().Be("A-104");
        UnitStatus(t, A104).Should().Be(StorageUnitStatus.Occupied, "the current renter is still inside; the unit is held for later");
    }

    [Fact]
    public async Task A_renter_still_inside_after_the_end_date_blocks_the_unit()
    {
        using var t = Create();
        RentOut(t, A104, MoveIn.AddDays(-90), MoveIn.AddDays(-30), ContractStatus.Overstay);

        var result = await As(t, Customer).CreateAsync(new CreateReservationRequest(FacilityId, LargeTypeId, MoveIn, 3), default);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Two_bookings_for_the_same_days_get_different_units()
    {
        using var t = Create();

        var first = await As(t, Customer).CreateAsync(new CreateReservationRequest(FacilityId, SmallTypeId, MoveIn, 3), default);
        var second = await As(t, OtherCustomer).CreateAsync(new CreateReservationRequest(FacilityId, SmallTypeId, MoveIn.AddMonths(1), 3), default);
        var third = await As(t, Customer).CreateAsync(new CreateReservationRequest(FacilityId, SmallTypeId, MoveIn.AddMonths(2), 1), default);
        var later = await As(t, OtherCustomer).CreateAsync(new CreateReservationRequest(FacilityId, SmallTypeId, MoveIn.AddMonths(4), 1), default);

        first.Value!.UnitCode.Should().Be("A-101");
        second.Value!.UnitCode.Should().Be("A-102");
        third.Error!.Type.Should().Be(ErrorType.Conflict, "both small units are taken in month three");
        later.Value!.UnitCode.Should().Be("A-101", "A-101 is free again once the first stay ends");
    }

    [Fact]
    public async Task Staff_choosing_another_unit_asks_the_customer_who_can_accept()
    {
        using var t = Create();
        var id = await Reserve(t);

        var assigned = await As(t, Staff).AssignUnitAsync(id, new AssignUnitRequest(A102), default);
        var accepted = await As(t, Customer).AcceptProposedUnitAsync(id, default);

        assigned.Value!.Status.Should().Be(ReservationStatus.Pending, "the customer was promised A-101 and must agree first");
        assigned.Value.ProposedUnitCode.Should().Be("A-102");
        accepted.Value!.Status.Should().Be(ReservationStatus.Confirmed);
        accepted.Value.UnitCode.Should().Be("A-102");
        accepted.Value.ProposedStorageUnitId.Should().BeNull();
        UnitStatus(t, A101).Should().Be(StorageUnitStatus.Available);
        UnitStatus(t, A102).Should().Be(StorageUnitStatus.Reserved);
    }

    [Fact]
    public async Task A_unit_offered_to_one_booking_is_not_given_to_another()
    {
        using var t = Create();
        var id = await Reserve(t);
        await As(t, Staff).AssignUnitAsync(id, new AssignUnitRequest(A102), default);

        var second = await As(t, OtherCustomer).CreateAsync(new CreateReservationRequest(FacilityId, SmallTypeId, MoveIn, 3), default);

        second.Error!.Type.Should().Be(ErrorType.Conflict, "A-101 is held and A-102 is offered to the first customer");
    }

    [Fact]
    public async Task Declining_the_proposed_unit_cancels_with_the_whole_deposit_refunded()
    {
        using var t = Create();
        t.Db.Add(new PricingPolicy { CreatedBy = StaffId, Version = 1, EffectiveFrom = new DateOnly(2025, 1, 1), DepositMonths = 1, CancelFreeHours = 0, Status = PricingPolicyStatus.Active,
            UnitPrices = { new UnitPrice { UnitTypeId = SmallTypeId, BasePricePerMonth = 500_000m } } });
        t.Db.SaveChanges();
        var id = await Reserve(t);
        var deposit = t.Db.Invoices.Single(i => i.ReservationId == id);
        deposit.PaidAmount = deposit.TotalAmount;
        deposit.Status = InvoiceStatus.Paid;
        t.Db.SaveChanges();
        await As(t, Staff).AssignUnitAsync(id, new AssignUnitRequest(A102), default);

        var declined = await As(t, Customer).DeclineProposedUnitAsync(id, default);

        declined.Value!.DepositRefundable.Should().BeTrue("the free-cancellation window has passed, but the change was ours");
        declined.Value.Reservation.Status.Should().Be(ReservationStatus.Cancelled);
        t.Db.DepositRefunds.Should().ContainSingle(r => r.ReservationId == id);
        UnitStatus(t, A101).Should().Be(StorageUnitStatus.Available);
    }

    [Fact]
    public async Task A_missed_appointment_expires_the_booking_and_keeps_the_deposit()
    {
        using var t = Create();
        var id = await Reserve(t);
        var set = await As(t, Customer).SetAppointmentAsync(id, new SetAppointmentRequest(MoveIn.ToDateTime(new TimeOnly(9, 0))), default);

        var (expired, _) = await As(t, Staff).RunDailyAsync(MoveIn.AddDays(1), 10, default);

        set.Value!.HandoverSlot.Should().Be(VietnamTime.ToUtc(MoveIn.ToDateTime(new TimeOnly(9, 0))), "stored in UTC");
        expired.Should().Be(1);
        t.Db.ChangeTracker.Clear();
        t.Db.Reservations.Find(id)!.Status.Should().Be(ReservationStatus.Expired);
        t.Db.DepositRefunds.Should().BeEmpty("a no-show forfeits the deposit");
        UnitStatus(t, A101).Should().Be(StorageUnitStatus.Available);
    }

    [Fact]
    public async Task An_appointment_after_move_in_or_outside_opening_hours_is_refused()
    {
        using var t = Create();
        var id = await Reserve(t);

        var late = await As(t, Customer).SetAppointmentAsync(id, new SetAppointmentRequest(MoveIn.AddDays(1).ToDateTime(new TimeOnly(9, 0))), default);
        var night = await As(t, Customer).SetAppointmentAsync(id, new SetAppointmentRequest(MoveIn.ToDateTime(new TimeOnly(22, 0))), default);

        late.Error!.Type.Should().Be(ErrorType.Validation);
        night.Error!.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task The_appointment_is_picked_with_the_booking_and_follows_the_same_rules()
    {
        using var t = Create();
        var nine = MoveIn.ToDateTime(new TimeOnly(9, 0));

        var booked = await As(t, Customer).CreateAsync(new CreateReservationRequest(FacilityId, SmallTypeId, MoveIn, 3, nine), default);
        var night = await As(t, Customer).CreateAsync(new CreateReservationRequest(FacilityId, SmallTypeId, MoveIn, 3, MoveIn.ToDateTime(new TimeOnly(22, 0))), default);
        var late = await As(t, Customer).CreateAsync(new CreateReservationRequest(FacilityId, SmallTypeId, MoveIn, 3, nine.AddDays(1)), default);

        booked.Value!.HandoverSlot.Should().Be(VietnamTime.ToUtc(nine), "stored in UTC");
        night.Error!.Type.Should().Be(ErrorType.Validation);
        late.Error!.Type.Should().Be(ErrorType.Validation);
        t.Db.Reservations.Should().ContainSingle("a refused time books nothing");
    }

    [Theory]
    [InlineData(0, InvoiceStatus.Issued, false)]
    [InlineData(400_000, InvoiceStatus.Issued, false)]
    [InlineData(0, InvoiceStatus.Overdue, false)]
    [InlineData(900_000, InvoiceStatus.Paid, true)]
    public async Task An_appointment_needs_the_deposit_paid(int paid, InvoiceStatus status, bool allowed)
    {
        using var t = Create();
        var id = await Reserve(t);
        t.Db.Add(new Invoice
        {
            ReservationId = id, CustomerId = CustomerId, InvoiceNumber = "DEP-APPOINTMENT", IssueDate = VietnamTime.Today,
            DueDate = MoveIn, TotalAmount = 900_000m, PaidAmount = paid, Status = status,
        });
        await t.Db.SaveChangesAsync();

        foreach (var user in new[] { Customer, Staff })
        {
            var set = await As(t, user).SetAppointmentAsync(id, new SetAppointmentRequest(MoveIn.ToDateTime(new TimeOnly(9, 0))), default);

            set.IsSuccess.Should().Be(allowed, "neither the customer nor staff book a handover before the deposit is paid");
            if (!allowed)
                set.Error!.Message.Should().Be("The deposit has not been paid yet.");
        }
    }

    [Fact]
    public async Task An_appointment_time_that_has_passed_is_refused()
    {
        using var t = Create();
        var id = await Reserve(t);

        var past = await As(t, Customer).SetAppointmentAsync(
            id, new SetAppointmentRequest(DateTime.SpecifyKind(DateTime.UtcNow.AddHours(7).AddMinutes(-1), DateTimeKind.Unspecified)), default);

        past.Error!.Message.Should().Contain("already passed");
    }

    [Fact]
    public async Task A_missed_appointment_cannot_be_moved()
    {
        using var t = Create();
        var id = await Reserve(t);
        t.Db.Reservations.Find(id)!.HandoverSlot = DateTime.UtcNow.AddMinutes(-1);
        await t.Db.SaveChangesAsync();

        var moved = await As(t, Customer).SetAppointmentAsync(id, new SetAppointmentRequest(MoveIn.ToDateTime(new TimeOnly(9, 0))), default);

        moved.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task A_booking_whose_unit_stays_rented_is_offered_another_unit()
    {
        using var t = Create();
        var id = await Reserve(t);
        // The renter of A-101 never left: their contract runs past the booking's move-in.
        RentOut(t, A101, MoveIn.AddDays(-90), MoveIn.AddDays(-30), ContractStatus.Overstay);

        var (_, proposed) = await As(t, Staff).RunDailyAsync(MoveIn.AddDays(-5), 10, default);

        proposed.Should().Be(1);
        var dto = await As(t, Customer).GetAsync(id, default);
        dto.Value!.ProposedUnitCode.Should().Be("A-102");
    }

    [Fact]
    public async Task A_bookings_own_draft_contract_does_not_block_it()
    {
        using var t = Create();
        var (id, _) = SeedReadyForHandover(t, signed: true);

        var (_, proposed) = await As(t, Staff).RunDailyAsync(MoveIn.AddDays(-1), 10, default);

        proposed.Should().Be(0, "the draft is this booking's own paperwork");
        t.Db.ChangeTracker.Clear();
        t.Db.Reservations.Find(id)!.ProposedStorageUnitId.Should().BeNull();
    }

    [Fact]
    public async Task Accepting_an_offer_moves_the_draft_contract_so_the_handover_works()
    {
        using var t = Create();
        var (id, contractId) = SeedReadyForHandover(t, signed: true);
        RentOut(t, A101, MoveIn.AddDays(-90), MoveIn.AddDays(30), ContractStatus.Suspended);

        (await As(t, Staff).RunDailyAsync(MoveIn.AddDays(-1), 10, default)).Proposed.Should().Be(1, "another renter still blocks A-101");
        t.Db.ChangeTracker.Clear();
        (await As(t, Customer).AcceptProposedUnitAsync(id, default)).Value!.StorageUnitId.Should().Be(A102);

        t.Db.ChangeTracker.Clear();
        var draft = t.Db.Contracts.Find(contractId)!;
        draft.StorageUnitId.Should().Be(A102);
        draft.SignedAt.Should().BeNull("the signature named the old unit");
        var contracts = new ContractService(t.Uow, Staff,
            new AccessCodeService(t.Uow, new Pbkdf2PasswordHasher(), Staff, TestCodes.Protector));
        (await contracts.SignAsync(contractId, default)).IsSuccess.Should().BeTrue();
        var handover = await new HandoverService(t.Uow, Staff, contracts)
            .CreateAsync(new CreateHandoverRequest(id, contractId, "LK-1", null, null), default);
        handover.IsSuccess.Should().BeTrue(handover.Error?.Message);
    }

    [Fact]
    public async Task Accepting_a_unit_still_rented_until_move_in_ends_the_draft_to_be_drawn_up_again()
    {
        using var t = Create();
        var (id, contractId) = SeedReadyForHandover(t, signed: true);
        // A-102 is free for the stay, but its renter's Active contract is the one the unit may carry until then.
        RentOut(t, A102, MoveIn.AddDays(-30), MoveIn.AddDays(-1));
        t.Db.Reservations.Find(id)!.ProposedStorageUnitId = A102;
        t.Db.SaveChanges();
        t.Db.ChangeTracker.Clear();

        var accepted = await As(t, Customer).AcceptProposedUnitAsync(id, default);

        accepted.Value!.StorageUnitId.Should().Be(A102);
        accepted.Value.ContractId.Should().BeNull("the booking waits for a new draft");
        t.Db.ChangeTracker.Clear();
        t.Db.Contracts.Find(contractId)!.Status.Should().Be(ContractStatus.Terminated);
        UnitStatus(t, A102).Should().Be(StorageUnitStatus.Occupied, "the renter is still inside");
    }
}
