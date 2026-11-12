using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Billing;
using StorEase.Application.Features.Invoices;
using StorEase.Application.Features.Pricing;
using StorEase.Application.Features.Reservations;
using StorEase.Application.Features.Reservations.Dtos;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

public class ScopeTermsAndBillingTests
{
    private static PricingPolicy ActivePolicy() => new()
    {
        CreatedBy = StaffId, Version = 1, EffectiveFrom = new DateOnly(2025, 1, 1), DepositMonths = 1, GraceDays = 5,
        NoticeDays = 30, LateFeePercentPerWeek = 2, SuspendAfterDays = 14, ClearOutAfterDays = 60, CancelFreeHours = 24,
        Status = PricingPolicyStatus.Active,
    };

    [Fact]
    public async Task Staff_see_and_assign_reservations_only_at_their_facility()
    {
        using var t = Create();
        t.Db.AddRange(
            new Facility { FacilityId = 2, Code = "TD", Name = "Thanh Đa", Address = "2 Test", District = "Bình Thạnh", Status = FacilityStatus.Active },
            new Floor { FloorId = 2, FacilityId = 2, FloorNumber = 1 },
            new StorageUnit { StorageUnitId = 20, FacilityId = 2, FloorId = 2, UnitTypeId = SmallTypeId, UnitCode = "T-101", Status = StorageUnitStatus.Available },
            new Reservation
            {
                ReservationId = 50, CustomerId = CustomerId, FacilityId = 2, UnitTypeId = SmallTypeId, ReservationCode = "RS-ELSEWHERE",
                MoveInDate = MoveIn, RentalMonths = 1, Status = ReservationStatus.Pending, CreatedAt = DateTime.UtcNow,
            });
        t.Db.SaveChanges();
        var email = new FakeEmailService();
        var staff = new ReservationService(t.Uow, Staff, email);
        var admin = new ReservationService(t.Uow, new FakeCurrentUser(99, Roles.SystemAdministrator), email);

        var seen = await staff.GetAsync(50, default);
        var assigned = await staff.AssignUnitAsync(50, new AssignUnitRequest(20), default);
        var adminSees = await admin.GetAsync(50, default);

        seen.Error!.Type.Should().Be(ErrorType.NotFound, "staff of Gò Vấp do not work at Thanh Đa");
        assigned.Error!.Type.Should().Be(ErrorType.NotFound);
        adminSees.IsSuccess.Should().BeTrue();
        (await t.Db.StorageUnits.SingleAsync(u => u.StorageUnitId == 20)).Status.Should().Be(StorageUnitStatus.Available);
    }

    [Fact]
    public async Task Current_terms_come_from_the_policy_in_force_with_discounts_per_size()
    {
        using var t = Create();
        var pricing = new PricingService(t.Uow, Customer);
        var none = await pricing.GetCurrentTermsAsync(default);

        t.Db.Add(new UnitPrice { PricingPolicy = ActivePolicy(), UnitTypeId = SmallTypeId, BasePricePerMonth = 900_000m, Discount3Month = 5, Discount6Month = 10, Discount12Month = 15 });
        t.Db.SaveChanges();
        var terms = await pricing.GetCurrentTermsAsync(default);

        none.Error!.Type.Should().Be(ErrorType.NotFound);
        terms.Value!.LateFeePercentPerWeek.Should().Be(2);
        terms.Value.SuspendAfterDays.Should().Be(14);
        terms.Value.CancelFreeHours.Should().Be(24);
        terms.Value.Discounts.Should().ContainSingle(d => d.UnitTypeId == SmallTypeId && d.Discount12Month == 15);
    }

    [Fact]
    public async Task A_contract_starting_on_the_31st_keeps_its_billing_day_after_february()
    {
        using var t = Create();
        t.Db.Add(ActivePolicy());
        var start = new DateOnly(2026, 1, 31);
        t.Db.Add(new Contract
        {
            Reservation = new Reservation
            {
                CustomerId = CustomerId, FacilityId = FacilityId, UnitTypeId = SmallTypeId, StorageUnitId = A101, ReservationCode = "RS-31",
                MoveInDate = start, RentalMonths = 3, Status = ReservationStatus.HandedOver, CreatedAt = DateTime.UtcNow,
            },
            CustomerId = CustomerId, StorageUnitId = A101, ContractNumber = "CT-31", StartDate = start, EndDate = start.AddMonths(3).AddDays(-1),
            MonthlyRent = 900_000m, DepositAmount = 900_000m, PaymentDay = 28, SignedAt = DateTime.UtcNow, Status = ContractStatus.Active,
        });
        t.Db.SaveChanges();
        var email = new FakeEmailService();

        await new BillingCycleService(t.Uow, new ReminderService(t.Uow, Staff, email)).RunAsync(new DateOnly(2026, 3, 31), default);

        var march = await t.Db.Invoices.Include(i => i.InvoiceItems).SingleAsync(i => i.InvoiceNumber.StartsWith("INV202603"));
        march.InvoiceItems.Single().Description.Should().Contain("31/03/2026", "the third period starts on the 31st, not the drifted 28th");
    }
}
