using FluentAssertions;
using StorEase.Application.Common;
using StorEase.Application.Features.Contracts;
using StorEase.Application.Features.Reports;
using StorEase.Application.Features.Returns;
using StorEase.Application.Features.Returns.Dtos;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

public class RentalEndTests
{
    private static RentalEndService As(TestDb t, Application.Interfaces.ICurrentUser user) => new(t.Uow, user, new FakeEmailService());

    private static readonly DateOnly Today = VietnamTime.Today;

    /// <summary>Customer rents A-101 (900,000/month, 900,000 deposit) until <paramref name="end"/>, with a working access code.</summary>
    private static int Rent(TestDb t, DateOnly end)
    {
        t.Db.Add(new PricingPolicy
        {
            CreatedBy = StaffId, Version = 1, EffectiveFrom = new DateOnly(2025, 1, 1), DepositMonths = 1, Status = PricingPolicyStatus.Active,
            EndReminderDays = 10, OverstayFeePercent = 150, MoveOutAfterDays = 7,
        });
        var booking = new Reservation
        {
            CustomerId = CustomerId, FacilityId = FacilityId, UnitTypeId = SmallTypeId, StorageUnitId = A101, ReservationCode = "RS-END",
            MoveInDate = end.AddMonths(-3), RentalMonths = 3, Status = ReservationStatus.HandedOver, CreatedAt = DateTime.UtcNow,
        };
        t.Db.Add(booking);
        t.Db.SaveChanges();
        var contract = new Contract
        {
            ReservationId = booking.ReservationId, CustomerId = CustomerId, StorageUnitId = A101, ContractNumber = "CT-END",
            StartDate = end.AddMonths(-3), EndDate = end, MonthlyRent = 900_000m, DepositAmount = 900_000m, PaymentDay = 5,
            Status = ContractStatus.Active,
            AccessCodes = { new AccessCode { CodeHash = "h", IssuedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(90), IsActive = true } },
        };
        t.Db.Add(contract);
        t.Db.StorageUnits.Find(A101)!.Status = StorageUnitStatus.Occupied;
        t.Db.SaveChanges();
        t.Db.ChangeTracker.Clear();
        return contract.ContractId;
    }

    [Fact]
    public async Task The_end_reminder_goes_out_once_ten_days_before()
    {
        using var t = Create();
        var id = Rent(t, Today.AddDays(10));

        var first = await As(t, Staff).RunAsync(Today, default);
        var again = await As(t, Staff).RunAsync(Today, default);
        var list = await As(t, Staff).ListAsync(false, default);

        first.Reminded.Should().Be(1);
        again.Reminded.Should().Be(0, "the reminder is sent once");
        list.Value!.Should().ContainSingle(r => r.ContractId == id && r.DaysLeft == 10 && r.Decision == "Undecided");
    }

    [Fact]
    public async Task Past_the_end_date_access_stops_and_the_overstay_fee_grows_daily()
    {
        using var t = Create();
        var id = Rent(t, Today.AddDays(-1));

        var run = await As(t, Staff).RunAsync(Today, default);
        await As(t, Staff).RunAsync(Today.AddDays(2), default);

        run.Overstayed.Should().Be(1);
        t.Db.ChangeTracker.Clear();
        var contract = t.Db.Contracts.Find(id)!;
        contract.Status.Should().Be(ContractStatus.Overstay);
        t.Db.AccessCodes.Where(a => a.ContractId == id).Should().OnlyContain(a => !a.IsActive && a.SuspendedAt != null);
        // 3 days × 900,000 / 30 × 150 %.
        t.Db.Invoices.Single(i => i.InvoiceNumber == RentalEndService.OverstayNumber(id)).TotalAmount.Should().Be(135_000m);
    }

    [Fact]
    public async Task Moving_goods_out_settles_the_deposit_and_frees_the_unit_for_the_next_booking()
    {
        using var t = Create();
        var id = Rent(t, Today.AddDays(-8));
        await As(t, Staff).RunAsync(Today, default);
        t.Db.Add(new Reservation
        {
            CustomerId = OtherCustomerId, FacilityId = FacilityId, UnitTypeId = SmallTypeId, StorageUnitId = A101, ReservationCode = "RS-NEXT",
            MoveInDate = Today.AddDays(3), RentalMonths = 1, Status = ReservationStatus.Confirmed, CreatedAt = DateTime.UtcNow,
        });
        t.Db.SaveChanges();

        var moved = await As(t, Staff).MoveToCentralStorageAsync(id, default);

        moved.IsSuccess.Should().BeTrue(moved.Error?.Message);
        moved.Value!.Status.Should().Be(ContractStatus.Closed);
        t.Db.ChangeTracker.Clear();
        // Deposit 900,000 − 8 days × 45,000 overstay fee.
        t.Db.DepositRefunds.Should().ContainSingle(r => r.Amount == 540_000m);
        t.Db.UnitReturns.Should().ContainSingle(r => r.ContractId == id && r.Status == ReturnStatus.Completed);
        t.Db.StorageUnits.Find(A101)!.Status.Should().Be(StorageUnitStatus.Reserved, "the next customer's booking holds it");
        t.Db.Invoices.Where(i => i.ContractId == id).Should().OnlyContain(i => i.Status == InvoiceStatus.Paid,
            "the deposit paid the overstay fee, so nothing is left to pay online");
        var revenue = await new ReportService(t.Uow, Staff).RevenueAsync(Today.AddDays(-1), Today.AddDays(1), null, default);
        revenue.Value!.Collected.Should().Be(0m, "money taken from the deposit was collected when the deposit was paid");
    }

    [Fact]
    public async Task Unpaid_rent_at_the_clear_out_stage_ends_in_the_central_warehouse()
    {
        using var t = Create();
        var id = Rent(t, Today.AddMonths(2));
        var invoice = new Invoice
        {
            InvoiceNumber = "INV-LATE", ContractId = id, CustomerId = CustomerId, IssueDate = Today.AddDays(-20), DueDate = Today.AddDays(-13),
            TotalAmount = 600_000m, Status = InvoiceStatus.Overdue,
        };
        t.Db.Add(invoice);
        t.Db.SaveChanges();
        var overdue = new OverdueCase
        {
            ContractId = id, InvoiceId = invoice.InvoiceId, OverdueSince = Today.AddDays(-12), DaysOverdue = 13,
            Stage = OverdueStage.Suspended, AccessSuspendedAt = DateTime.UtcNow,
        };
        t.Db.Add(overdue);
        t.Db.Contracts.Find(id)!.Status = ContractStatus.Suspended;
        t.Db.SaveChanges();

        var early = await As(t, Staff).MoveToCentralStorageAsync(id, default);
        t.Db.OverdueCases.Find(overdue.OverdueCaseId)!.Stage = OverdueStage.ClearOut;
        t.Db.SaveChanges();
        t.Db.ChangeTracker.Clear();
        var moved = await As(t, Staff).MoveToCentralStorageAsync(id, default);

        early.Error!.Type.Should().Be(ErrorType.Conflict, "a locked-out renter keeps the goods until the clear-out stage");
        moved.IsSuccess.Should().BeTrue(moved.Error?.Message);
        t.Db.ChangeTracker.Clear();
        t.Db.Contracts.Find(id)!.Status.Should().Be(ContractStatus.Closed);
        t.Db.Contracts.Find(id)!.MovedToCentralStorageAt.Should().NotBeNull();
        t.Db.OverdueCases.Find(overdue.OverdueCaseId)!.ResolvedAt.Should().NotBeNull();
        // Deposit 900,000 − unpaid rent 600,000.
        t.Db.DepositRefunds.Should().ContainSingle(r => r.Amount == 300_000m);
    }

    [Fact]
    public async Task A_renewal_still_waiting_keeps_the_rental_running_past_its_end_date()
    {
        using var t = Create();
        var id = Rent(t, Today.AddDays(-1));
        t.Db.Add(new Domain.Entities.Renewal { ContractId = id, RequestedAt = DateTime.UtcNow, ExtendMonths = 3, NewEndDate = Today.AddMonths(3),
            NewMonthlyRent = 900_000m, Status = Features.Renewals.RenewalStatus.Requested });
        t.Db.SaveChanges();

        var run = await As(t, Staff).RunAsync(Today, default);

        run.Overstayed.Should().Be(0);
        t.Db.ChangeTracker.Clear();
        t.Db.Contracts.Find(id)!.Status.Should().Be(ContractStatus.Active);
    }

    [Fact]
    public async Task Goods_stay_until_the_grace_days_have_passed()
    {
        using var t = Create();
        var id = Rent(t, Today.AddDays(-3));
        await As(t, Staff).RunAsync(Today, default);

        var moved = await As(t, Staff).MoveToCentralStorageAsync(id, default);

        moved.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task An_overstaying_renter_can_still_hand_back_and_gets_access_to_empty_the_unit()
    {
        using var t = Create();
        var id = Rent(t, Today.AddDays(-2));
        await As(t, Staff).RunAsync(Today, default);

        var requested = await new ReturnService(t.Uow, Customer).RequestAsync(new CreateReturnRequest(id, Today.AddDays(1), "Late", null, null), default);
        var later = await As(t, Staff).RunAsync(Today.AddDays(3), default);

        requested.IsSuccess.Should().BeTrue(requested.Error?.Message);
        later.Overstayed.Should().Be(0);
        t.Db.ChangeTracker.Clear();
        t.Db.Contracts.Find(id)!.Status.Should().Be(ContractStatus.PendingReturn);
        t.Db.AccessCodes.Where(a => a.ContractId == id).Should().OnlyContain(a => a.IsActive && a.ExpiresAt > DateTime.UtcNow);
        t.Db.Invoices.Single(i => i.InvoiceNumber == RentalEndService.OverstayNumber(id)).TotalAmount.Should().Be(90_000m, "the fee stops once the return is requested");
    }
}
