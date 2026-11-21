using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Billing;
using StorEase.Application.Features.Renewals;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

/// <summary>T-01: a manager approves or rejects a customer's renewal request.</summary>
public class RenewalDecisionTests
{
    private static readonly DateOnly Start = new(2026, 1, 10);
    private static readonly DateOnly End = new(2026, 4, 9);
    private static readonly DateOnly NewEnd = new(2026, 7, 9);

    /// <summary>An Active contract on A-101 (10 Jan – 9 Apr 2026) with a live code and a Requested 3-month renewal at 800k.</summary>
    private static (TestDb Db, int RenewalId, int ContractId) Seed()
    {
        var t = Create();
        var contract = new Contract
        {
            Reservation = new Reservation
            {
                CustomerId = CustomerId, FacilityId = FacilityId, UnitTypeId = SmallTypeId, StorageUnitId = A101,
                ReservationCode = "RS-RENEW", MoveInDate = Start, RentalMonths = 3, Status = ReservationStatus.HandedOver, CreatedAt = DateTime.UtcNow,
            },
            CustomerId = CustomerId, StorageUnitId = A101, ContractNumber = "CT-RENEW", StartDate = Start, EndDate = End,
            MonthlyRent = 900_000m, DepositAmount = 900_000m, PaymentDay = 10, SignedAt = DateTime.UtcNow, Status = ContractStatus.Active,
        };
        contract.AccessCodes.Add(new AccessCode { CodeHash = "x", IssuedAt = DateTime.UtcNow, ExpiresAt = new DateTime(2026, 4, 10), IsActive = true });
        var renewal = new Renewal
        {
            Contract = contract, RequestedAt = DateTime.UtcNow, ExtendMonths = 3, NewEndDate = NewEnd,
            NewMonthlyRent = 800_000m, Status = RenewalStatus.Requested,
        };
        t.Db.Add(renewal);
        t.Db.SaveChanges();
        t.Db.ChangeTracker.Clear();
        return (t, renewal.RenewalId, contract.ContractId);
    }

    private static RenewalService As(TestDb t, int userId, string role) => new(t.Uow, new FakeCurrentUser(userId, role));

    [Fact]
    public async Task Approving_extends_the_contract_and_the_billing_job_bills_the_new_months()
    {
        var (t, renewalId, contractId) = Seed();
        using var _ = t;

        var result = await As(t, StaffId, Roles.FacilityManager).ApproveAsync(renewalId, default);
        var again = await As(t, StaffId, Roles.FacilityManager).ApproveAsync(renewalId, default);
        await new BillingCycleService(t.Uow, new ReminderService(t.Uow, Staff, new FakeEmailService()))
            .RunAsync(new DateOnly(2026, 5, 5), default);

        result.Value!.Status.Should().Be(RenewalStatus.Approved);
        again.Error!.Type.Should().Be(ErrorType.Conflict);
        var contract = await t.Db.Contracts.Include(c => c.AccessCodes).Include(c => c.Invoices).ThenInclude(i => i.InvoiceItems)
            .SingleAsync(c => c.ContractId == contractId);
        contract.EndDate.Should().Be(NewEnd);
        contract.MonthlyRent.Should().Be(800_000m);
        contract.AccessCodes.Single().ExpiresAt.Should().Be(new DateTime(2026, 7, 10));
        contract.Invoices.Should().Contain(i => i.DueDate == new DateOnly(2026, 5, 10) && i.TotalAmount == 800_000m,
            "May is past the old end date and is billed only because the renewal extended the contract");
    }

    [Fact]
    public async Task Rejecting_changes_nothing_and_another_facility_cannot_decide()
    {
        var (t, renewalId, contractId) = Seed();
        using var _ = t;

        var outsider = await As(t, 77, Roles.FacilityManager).ApproveAsync(renewalId, default);
        var rejected = await As(t, StaffId, Roles.FacilityManager).RejectAsync(renewalId, default);

        outsider.Error!.Type.Should().Be(ErrorType.NotFound, "manager 77 works at no facility");
        rejected.Value!.Status.Should().Be(RenewalStatus.Rejected);
        var contract = await t.Db.Contracts.SingleAsync(c => c.ContractId == contractId);
        contract.EndDate.Should().Be(End);
        contract.MonthlyRent.Should().Be(900_000m);
    }

    /// <summary>Another customer booked A-101 from the day after the current rental ends.</summary>
    private static void BookNext(TestDb t)
    {
        t.Db.Add(new Reservation
        {
            CustomerId = OtherCustomerId, FacilityId = FacilityId, UnitTypeId = SmallTypeId, StorageUnitId = A101, ReservationCode = "RS-NEXT",
            MoveInDate = End.AddDays(1), RentalMonths = 3, Status = ReservationStatus.Confirmed, CreatedAt = DateTime.UtcNow,
        });
        t.Db.SaveChanges();
        t.Db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task When_the_next_customer_booked_the_unit_the_renter_is_offered_another_and_moves_on_accepting()
    {
        var (t, renewalId, contractId) = Seed();
        using var _ = t;
        BookNext(t);

        var approved = await As(t, StaffId, Roles.FacilityManager).ApproveAsync(renewalId, default);
        var accepted = await As(t, CustomerId, Roles.StorageCustomer).AcceptTransferAsync(renewalId, default);

        approved.Value!.Status.Should().Be(RenewalStatus.AwaitingCustomer);
        approved.Value.TransferUnitCode.Should().Be("A-102");
        approved.Value.NewEndDate.Should().Be(NewEnd);
        accepted.Value!.Status.Should().Be(RenewalStatus.Approved);
        t.Db.ChangeTracker.Clear();
        var contract = t.Db.Contracts.Find(contractId)!;
        contract.StorageUnitId.Should().Be(A102);
        contract.EndDate.Should().Be(NewEnd);
        t.Db.StorageUnits.Find(A102)!.Status.Should().Be(StorageUnitStatus.Occupied);
        t.Db.StorageUnits.Find(A101)!.Status.Should().Be(StorageUnitStatus.Reserved, "the next customer's booking keeps A-101");
    }

    [Fact]
    public async Task Declining_the_offered_unit_drops_the_renewal_and_hands_back_on_the_end_date()
    {
        var (t, renewalId, contractId) = Seed();
        using var _ = t;
        // The rental ends 40 days from now, beyond the usual 15-day move-out window.
        var end = VietnamTime.Today.AddDays(40);
        var c = t.Db.Contracts.Find(contractId)!;
        c.StartDate = end.AddMonths(-3);
        c.EndDate = end;
        t.Db.Renewals.Find(renewalId)!.NewEndDate = end.AddMonths(3);
        t.Db.Add(new Reservation
        {
            CustomerId = OtherCustomerId, FacilityId = FacilityId, UnitTypeId = SmallTypeId, StorageUnitId = A101, ReservationCode = "RS-NEXT",
            MoveInDate = end.AddDays(1), RentalMonths = 3, Status = ReservationStatus.Confirmed, CreatedAt = DateTime.UtcNow,
        });
        t.Db.SaveChanges();
        t.Db.ChangeTracker.Clear();
        await As(t, StaffId, Roles.FacilityManager).ApproveAsync(renewalId, default);

        var declined = await As(t, CustomerId, Roles.StorageCustomer).DeclineTransferAsync(renewalId, default);

        declined.Value!.Status.Should().Be(RenewalStatus.Rejected);
        t.Db.ChangeTracker.Clear();
        var contract = t.Db.Contracts.Find(contractId)!;
        contract.StorageUnitId.Should().Be(A101);
        contract.EndDate.Should().Be(end);
        t.Db.UnitReturns.Should().ContainSingle(r => r.ContractId == contractId && r.PlannedReturnDate == end && r.MoveOutDeadline == end,
            "declining hands the unit back on the end date, however far off it is");
    }
}
