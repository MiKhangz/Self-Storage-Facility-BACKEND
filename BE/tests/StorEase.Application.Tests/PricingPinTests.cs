using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.AccessCodes;
using StorEase.Application.Features.Billing;
using StorEase.Application.Features.Contracts;
using StorEase.Application.Features.Contracts.Dtos;
using StorEase.Application.Features.Pricing;
using StorEase.Application.Features.Reservations;
using StorEase.Application.Features.Reservations.Dtos;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using StorEase.Infrastructure.Services;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

/// <summary>
/// Publishing a new pricing policy must not reach customers who booked or signed under the old one: their rent,
/// deposit, late-fee rules and early-return penalty stay those of the version they were quoted.
/// </summary>
public class PricingPinTests
{
    /// <summary>v1 (900k, 20 % early-return penalty, 5 grace days) superseded yesterday by v2 (500k, 0 %, 30 grace days).</summary>
    private static (TestDb T, int V1, int V2) TwoVersions()
    {
        var t = Create();
        var today = VietnamTime.Today;
        var v1 = new PricingPolicy
        {
            CreatedBy = StaffId, Version = 1, EffectiveFrom = new DateOnly(2025, 1, 1), EffectiveTo = today.AddDays(-1),
            DepositMonths = 1, GraceDays = 5, NoticeDays = 30, LateFeePercentPerWeek = 2, EarlyTerminationPercent = 20,
            SuspendAfterDays = 14, ClearOutAfterDays = 60, CancelFreeHours = 24, Status = PricingPolicyStatus.Superseded,
        };
        v1.UnitPrices.Add(new UnitPrice { UnitTypeId = SmallTypeId, BasePricePerMonth = 900_000m });
        var v2 = new PricingPolicy
        {
            CreatedBy = StaffId, Version = 2, EffectiveFrom = today, DepositMonths = 2, GraceDays = 30, NoticeDays = 30,
            LateFeePercentPerWeek = 9, EarlyTerminationPercent = 0, SuspendAfterDays = 60, ClearOutAfterDays = 90,
            CancelFreeHours = 24, Status = PricingPolicyStatus.Active,
        };
        v2.UnitPrices.Add(new UnitPrice { UnitTypeId = SmallTypeId, BasePricePerMonth = 500_000m });
        t.Db.AddRange(v1, v2);
        t.Db.SaveChanges();
        return (t, v1.PricingPolicyId, v2.PricingPolicyId);
    }

    [Fact]
    public async Task A_booking_quoted_under_the_old_price_gets_that_price_on_its_contract()
    {
        var (t, v1, v2) = TwoVersions();
        using var _ = t;
        // Booked last week at 900k; v2 (500k) was published before the move-in date.
        var reservation = new Reservation
        {
            CustomerId = CustomerId, FacilityId = FacilityId, UnitTypeId = SmallTypeId, StorageUnitId = A101, ReservationCode = "RS-PIN",
            MoveInDate = MoveIn, RentalMonths = 1, PricingPolicyId = v1, Status = ReservationStatus.Confirmed, CreatedAt = DateTime.UtcNow.AddDays(-7),
        };
        t.Db.Add(reservation);
        t.Db.SaveChanges();

        var drafted = await new ContractService(t.Uow, Staff, new AccessCodeService(t.Uow, new Pbkdf2PasswordHasher(), Staff))
            .CreateAsync(new CreateContractRequest(reservation.ReservationId, 5), default);
        var newBooking = await new ReservationService(t.Uow, Customer, new FakeEmailService())
            .CreateAsync(new CreateReservationRequest(FacilityId, SmallTypeId, MoveIn, 1), default);

        drafted.Value!.MonthlyRent.Should().Be(900_000m, "the customer was quoted v1");
        drafted.Value.DepositAmount.Should().Be(900_000m, "v1 asks one month's deposit, v2 two");
        (await t.Db.Contracts.SingleAsync()).PricingPolicyId.Should().Be(v1);
        (await t.Db.Reservations.SingleAsync(r => r.ReservationId == newBooking.Value!.ReservationId)).PricingPolicyId
            .Should().Be(v2, "a new booking is quoted from the version in force now");
    }

    [Fact]
    public async Task Overdue_rules_and_the_early_return_penalty_come_from_the_contracts_own_version()
    {
        var (t, v1, _) = TwoVersions();
        using var _ = t;
        var start = new DateOnly(2026, 1, 10);
        var contract = new Contract
        {
            Reservation = new Reservation
            {
                CustomerId = CustomerId, FacilityId = FacilityId, UnitTypeId = SmallTypeId, StorageUnitId = A101, ReservationCode = "RS-OLD",
                MoveInDate = start, RentalMonths = 3, PricingPolicyId = v1, Status = ReservationStatus.HandedOver, CreatedAt = DateTime.UtcNow,
            },
            CustomerId = CustomerId, StorageUnitId = A101, ContractNumber = "CT-OLD", StartDate = start, EndDate = start.AddMonths(3).AddDays(-1),
            MonthlyRent = 900_000m, DepositAmount = 900_000m, PaymentDay = 10, PricingPolicyId = v1,
            SignedAt = DateTime.UtcNow, Status = ContractStatus.Active,
        };
        t.Db.Add(contract);
        t.Db.SaveChanges();
        var billing = new BillingCycleService(t.Uow, new ReminderService(t.Uow, Staff, new FakeEmailService()));

        await billing.RunAsync(new DateOnly(2026, 1, 5), default);
        // 15 days past due: past v1's 5 grace days and 14-day lock-out, well inside v2's 30 and 60.
        var run = await billing.RunAsync(new DateOnly(2026, 1, 25), default);
        var undated = await RentCalculator.PolicyOfAsync(t.Uow, null, start, default);

        run.Value!.CasesOpened.Should().Be(1);
        run.Value.ContractsLockedOut.Should().Be(1, "the contract was signed under v1's 14-day lock-out");
        undated!.PricingPolicyId.Should().Be(v1, "a superseded version still governs the dates it covered");
        undated.EarlyTerminationPercent.Should().Be(20m);
    }
}
