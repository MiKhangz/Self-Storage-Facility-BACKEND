using FluentAssertions;
using StorEase.Application.Common;
using StorEase.Application.Features.Pricing;
using StorEase.Application.Features.Pricing.Dtos;
using StorEase.Application.Features.Reservations;
using StorEase.Application.Features.Reservations.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

/// <summary>
/// A facility may set its own price for a size inside a price version; everywhere else the company price applies.
/// </summary>
public class FacilityPriceTests
{
    private const int OtherFacilityId = 2;
    private static readonly ICurrentUser Ops = new FakeCurrentUser(StaffId, Roles.BusinessOperationsManager);

    /// <summary>
    /// Live version: 5 m² costs 500k company-wide (10 % off 3 months); Gò Vấp charges 800k (5 % off 3 months).
    /// A second facility has a free 5 m² unit and no price of its own.
    /// </summary>
    private static (TestDb T, int PolicyId) Priced()
    {
        var t = Create();
        t.Db.AddRange(
            new Facility { FacilityId = OtherFacilityId, Code = "TD", Name = "Thủ Đức", Address = "2 Test", District = "Thủ Đức", Status = FacilityStatus.Active },
            new Floor { FloorId = 2, FacilityId = OtherFacilityId, FloorNumber = 1 },
            new StorageUnit { StorageUnitId = 50, FacilityId = OtherFacilityId, FloorId = 2, UnitTypeId = SmallTypeId, UnitCode = "B-101", Status = StorageUnitStatus.Available },
            // A facility takes bookings only once someone works there.
            new StaffFacility { UserId = StaffId, FacilityId = OtherFacilityId, AssignedDate = new DateOnly(2026, 1, 1) });
        var policy = new PricingPolicy
        {
            CreatedBy = StaffId, Version = 1, EffectiveFrom = VietnamTime.Today.AddDays(-30), DepositMonths = 1, GraceDays = 5, NoticeDays = 30,
            LateFeePercentPerWeek = 2, SuspendAfterDays = 14, ClearOutAfterDays = 60, CancelFreeHours = 24, Status = PricingPolicyStatus.Active,
        };
        policy.UnitPrices.Add(new UnitPrice { UnitTypeId = SmallTypeId, BasePricePerMonth = 500_000m, Discount3Month = 10 });
        policy.UnitPrices.Add(new UnitPrice { UnitTypeId = SmallTypeId, FacilityId = FacilityId, BasePricePerMonth = 800_000m, Discount3Month = 5 });
        t.Db.Add(policy);
        t.Db.SaveChanges();
        t.Db.ChangeTracker.Clear();
        return (t, policy.PricingPolicyId);
    }

    [Fact]
    public async Task A_booking_is_priced_at_its_facility_and_falls_back_to_the_company_price_elsewhere()
    {
        var (t, _) = Priced();
        using var _db = t;
        var bookings = new ReservationService(t.Uow, Customer, new FakeEmailService());

        var ownPrice = await bookings.CreateAsync(new CreateReservationRequest(FacilityId, SmallTypeId, MoveIn, 3), default);
        var companyPrice = await new ReservationService(t.Uow, OtherCustomer, new FakeEmailService())
            .CreateAsync(new CreateReservationRequest(OtherFacilityId, SmallTypeId, MoveIn, 3), default);

        ownPrice.IsSuccess.Should().BeTrue(ownPrice.Error?.Message);
        companyPrice.IsSuccess.Should().BeTrue(companyPrice.Error?.Message);
        ownPrice.Value!.DepositAmount.Should().Be(760_000m, "one month's deposit at Gò Vấp's own 800k with its own 5 % off for 3 months");
        companyPrice.Value!.DepositAmount.Should().Be(450_000m, "Thủ Đức has no own price: 500k with the company's 10 % off");
    }

    [Fact]
    public async Task From_prices_take_the_lowest_anywhere_and_a_facility_sees_its_own()
    {
        var (t, policyId) = Priced();
        using var _db = t;

        (await RentCalculator.PricesAsync(t.Uow, policyId, null, default))[SmallTypeId].Should().Be(500_000m);
        (await RentCalculator.PricesAsync(t.Uow, policyId, FacilityId, default))[SmallTypeId].Should().Be(800_000m);
        (await RentCalculator.PricesAsync(t.Uow, policyId, OtherFacilityId, default))[SmallTypeId].Should().Be(500_000m);
        (await RentCalculator.PriceAsync(t.Uow, policyId, SmallTypeId, OtherFacilityId, default))!.FacilityId.Should().BeNull();
    }

    [Fact]
    public async Task A_draft_copies_facility_prices_which_ops_can_change_or_remove_but_a_live_version_stays()
    {
        var (t, policyId) = Priced();
        using var _db = t;
        var pricing = new PricingService(t.Uow, Ops);

        var draft = (await pricing.CopyToDraftAsync(policyId, default)).Value!;
        var onLive = await pricing.UpsertFacilityPriceAsync(policyId, OtherFacilityId, SmallTypeId, new UpsertUnitPriceRequest(600_000m, 0, 0, 0), default);
        var set = await pricing.UpsertFacilityPriceAsync(draft.PricingPolicyId, OtherFacilityId, SmallTypeId, new UpsertUnitPriceRequest(600_000m, 0, 0, 0), default);
        var removed = await pricing.DeleteFacilityPriceAsync(draft.PricingPolicyId, FacilityId, SmallTypeId, default);
        var after = (await pricing.GetPolicyAsync(draft.PricingPolicyId, default)).Value!;

        draft.FacilityPrices.Should().ContainSingle(p => p.FacilityId == FacilityId && p.BasePricePerMonth == 800_000m && p.FacilityName == "Gò Vấp");
        draft.UnitPrices.Should().ContainSingle(p => p.BasePricePerMonth == 500_000m, "company prices are listed apart from facility prices");
        onLive.Error!.Type.Should().Be(ErrorType.Conflict, "a live version is history");
        set.Value!.FacilityName.Should().Be("Thủ Đức");
        removed.IsSuccess.Should().BeTrue();
        after.FacilityPrices.Should().ContainSingle(p => p.FacilityId == OtherFacilityId && p.BasePricePerMonth == 600_000m);
    }
}
