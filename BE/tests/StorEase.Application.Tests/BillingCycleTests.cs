using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Billing;
using StorEase.Application.Features.Invoices;
using StorEase.Application.Features.Invoices.Dtos;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

public class BillingCycleTests
{
    private static readonly DateOnly Start = new(2026, 1, 10);

    /// <summary>An Active 3-month contract on A-101 from 10 Jan 2026, paying on the 10th, with one live access code.</summary>
    private static (TestDb Db, int ContractId, FakeEmailService Email) Seed()
    {
        var t = Create();
        t.Db.Add(new PricingPolicy
        {
            CreatedBy = StaffId, Version = 1, EffectiveFrom = new DateOnly(2025, 1, 1), DepositMonths = 1, GraceDays = 5,
            NoticeDays = 30, LateFeePercentPerWeek = 2, SuspendAfterDays = 14, ClearOutAfterDays = 60, CancelFreeHours = 24,
            Status = PricingPolicyStatus.Active,
        });
        var reservation = new Reservation
        {
            CustomerId = CustomerId, FacilityId = FacilityId, UnitTypeId = SmallTypeId, StorageUnitId = A101,
            ReservationCode = "RS-BILL", MoveInDate = Start, RentalMonths = 3, Status = ReservationStatus.CheckedIn, CreatedAt = DateTime.UtcNow,
        };
        var contract = new Contract
        {
            Reservation = reservation, CustomerId = CustomerId, StorageUnitId = A101, ContractNumber = "CT-BILL",
            StartDate = Start, EndDate = Start.AddMonths(3).AddDays(-1), MonthlyRent = 900_000m, DepositAmount = 900_000m,
            PaymentDay = 10, SignedAt = DateTime.UtcNow, Status = ContractStatus.Active,
        };
        contract.AccessCodes.Add(new AccessCode
        {
            CodeHash = "x", IssuedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddYears(1), IsActive = true,
        });
        t.Db.Add(contract);
        t.Db.SaveChanges();
        t.Db.ChangeTracker.Clear();
        return (t, contract.ContractId, new FakeEmailService());
    }

    private static BillingCycleService Billing(TestDb t, FakeEmailService email) =>
        new(t.Uow, new ReminderService(t.Uow, Staff, email));

    [Fact]
    public async Task Rent_invoices_are_issued_a_week_ahead_and_only_once()
    {
        var (t, contractId, email) = Seed();
        using var _ = t;

        var first = await Billing(t, email).RunAsync(new DateOnly(2026, 1, 5), default);
        var again = await Billing(t, email).RunAsync(new DateOnly(2026, 1, 5), default);
        var nextMonth = await Billing(t, email).RunAsync(new DateOnly(2026, 2, 3), default);

        first.Value!.InvoicesIssued.Should().Be(1);
        again.Value!.InvoicesIssued.Should().Be(0, "the job is idempotent");
        nextMonth.Value!.InvoicesIssued.Should().Be(1);
        var invoices = await t.Db.Invoices.Include(i => i.InvoiceItems).Where(i => i.ContractId == contractId).OrderBy(i => i.DueDate).ToListAsync();
        invoices.Select(i => i.DueDate).Should().Equal(new DateOnly(2026, 1, 10), new DateOnly(2026, 2, 10));
        invoices.Should().OnlyContain(i => i.InvoiceItems.First().LineAmount == 900_000m);
    }

    [Fact]
    public async Task An_unpaid_invoice_escalates_to_late_fee_and_lockout_and_paying_lifts_it()
    {
        var (t, contractId, email) = Seed();
        using var _ = t;
        await Billing(t, email).RunAsync(new DateOnly(2026, 1, 5), default);

        // 15 days past due: one full week past the 5-day grace, and past the 14-day lockout.
        var run = await Billing(t, email).RunAsync(new DateOnly(2026, 1, 25), default);

        run.Value!.CasesOpened.Should().Be(1);
        run.Value.ContractsLockedOut.Should().Be(1);
        var invoice = await t.Db.Invoices.Include(i => i.InvoiceItems).SingleAsync(i => i.ContractId == contractId);
        invoice.Status.Should().Be(InvoiceStatus.Overdue);
        invoice.TotalAmount.Should().Be(918_000m, "2% of 900,000 for one week");
        var overdue = await t.Db.OverdueCases.SingleAsync();
        overdue.Stage.Should().Be(OverdueStage.Suspended);
        overdue.LateFeeAmount.Should().Be(18_000m);
        (await t.Db.Contracts.FindAsync(contractId))!.Status.Should().Be(ContractStatus.Suspended);
        (await t.Db.AccessCodes.SingleAsync()).IsActive.Should().BeFalse();
        t.Db.ChangeTracker.Clear();

        var bom = new InvoiceService(t.Uow, new FakeCurrentUser(StaffId, Roles.BusinessOperationsManager), new PaymentGatewayOptions());
        var paid = await bom.RecordManualPaymentAsync(invoice.InvoiceId, new PaymentRequest(918_000m, "CASH"), default);

        paid.Value!.Status.Should().Be(InvoiceStatus.Paid);
        t.Db.ChangeTracker.Clear();
        (await t.Db.OverdueCases.SingleAsync()).Stage.Should().Be(OverdueStage.Resolved);
        (await t.Db.Contracts.FindAsync(contractId))!.Status.Should().Be(ContractStatus.Active);
        (await t.Db.AccessCodes.SingleAsync()).IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData(ContractStatus.Closed)]
    [InlineData(ContractStatus.Terminated)]
    public async Task A_closed_or_cancelled_rental_is_not_chased_for_its_open_invoice(ContractStatus status)
    {
        var (t, contractId, email) = Seed();
        using var _ = t;
        await Billing(t, email).RunAsync(new DateOnly(2026, 1, 5), default);
        var contract = await t.Db.Contracts.SingleAsync(c => c.ContractId == contractId);
        contract.Status = status;
        await t.Db.SaveChangesAsync();

        var run = await Billing(t, email).RunAsync(new DateOnly(2026, 2, 25), default);

        run.Value!.CasesOpened.Should().Be(0);
        run.Value.ContractsLockedOut.Should().Be(0);
        (await t.Db.OverdueCases.CountAsync()).Should().Be(0);
        (await t.Db.Contracts.SingleAsync(c => c.ContractId == contractId)).Status.Should().Be(status);
    }

    [Fact]
    public async Task Due_soon_reminders_go_out_once_and_respect_the_opt_out()
    {
        var (t, _, email) = Seed();
        using var __ = t;
        await Billing(t, email).RunAsync(new DateOnly(2026, 1, 5), default);

        var run = await Billing(t, email).RunAsync(new DateOnly(2026, 1, 7), default);
        var rerun = await Billing(t, email).RunAsync(new DateOnly(2026, 1, 7), default);

        run.Value!.RemindersSent.Should().Be(1);
        rerun.Value!.RemindersSent.Should().Be(0);
        email.Sent.Should().ContainSingle().Which.To.Should().Be("customer@x.vn");

        t.Db.Reminders.RemoveRange(t.Db.Reminders);
        t.Db.NotificationPreferences.Add(new NotificationPreference { UserId = CustomerId, EmailReminders = false, UpdatedAt = DateTime.UtcNow });
        await t.Db.SaveChangesAsync();
        var optedOut = await Billing(t, email).RunAsync(new DateOnly(2026, 1, 7), default);

        optedOut.Value!.RemindersSent.Should().Be(0);
    }
}
