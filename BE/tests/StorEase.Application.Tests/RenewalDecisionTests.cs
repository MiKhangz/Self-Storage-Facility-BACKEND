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
}
