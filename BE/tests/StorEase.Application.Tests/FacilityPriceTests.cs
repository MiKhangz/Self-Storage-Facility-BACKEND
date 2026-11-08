using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.AccessCodes;
using StorEase.Application.Features.Contracts;
using StorEase.Application.Features.Contracts.Dtos;
using StorEase.Application.Features.Pricing;
using StorEase.Application.Features.Pricing.Dtos;
using StorEase.Application.Features.Reservations;
using StorEase.Application.Features.Reservations.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using StorEase.Infrastructure.Services;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

/// <summary>
/// Each facility keeps its own price list in versions. A size it prices costs that there; every other size, and every
/// rental rule, comes from the company standard. A booking keeps the price it was quoted.
/// </summary>
public class FacilityPriceTests
{
    private const int OtherFacilityId = 2;
    private static readonly ICurrentUser Ops = new FakeCurrentUser(StaffId, Roles.BusinessOperationsManager);
    private static UpsertUnitPriceRequest Price(decimal amount, decimal d3 = 0) => new(amount, d3, 0, 0);

    /// <summary>Standard: 5 m² at 500k (10 % off 3 months). Gò Vấp publishes its own 800k (5 % off 3 months).</summary>
    private static async Task<TestDb> PricedAsync()
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
        t.Db.Add(policy);
        t.Db.SaveChanges();
        t.Db.ChangeTracker.Clear();

        await PublishAsync(t, FacilityId, 800_000m, 5);
        return t;
    }

    private static async Task<FacilityPriceVersionDto> PublishAsync(TestDb t, int facilityId, decimal price, decimal d3 = 0)
    {
        var prices = new FacilityPricingService(t.Uow, Ops);
        var draft = (await prices.CreateDraftAsync(facilityId, default)).Value!;
        (await prices.SetPriceAsync(facilityId, draft.FacilityPriceVersionId, SmallTypeId, Price(price, d3), default)).IsSuccess.Should().BeTrue();
        return (await prices.PublishAsync(facilityId, draft.FacilityPriceVersionId, default)).Value!;
    }

    [Fact]
    public async Task A_booking_uses_its_facilitys_own_price_and_other_facilities_use_the_standard()
    {
        using var t = await PricedAsync();

        var own = await new ReservationService(t.Uow, Customer, new FakeEmailService())
            .CreateAsync(new CreateReservationRequest(FacilityId, SmallTypeId, MoveIn, 3), default);
        var standard = await new ReservationService(t.Uow, OtherCustomer, new FakeEmailService())
            .CreateAsync(new CreateReservationRequest(OtherFacilityId, SmallTypeId, MoveIn, 3), default);

        own.IsSuccess.Should().BeTrue(own.Error?.Message);
        standard.IsSuccess.Should().BeTrue(standard.Error?.Message);
        own.Value!.DepositAmount.Should().Be(760_000m, "one month's deposit at Gò Vấp's own 800k, 5 % off for 3 months");
        standard.Value!.DepositAmount.Should().Be(450_000m, "Thủ Đức has no list: the standard 500k, 10 % off");
        (await t.Db.Reservations.SingleAsync(r => r.ReservationId == own.Value.ReservationId)).FacilityPriceVersionId.Should().NotBeNull();
        (await t.Db.Reservations.SingleAsync(r => r.ReservationId == standard.Value.ReservationId)).FacilityPriceVersionId.Should().BeNull();
    }

    [Fact]
    public async Task A_booking_keeps_its_quoted_price_when_the_facility_publishes_a_new_one()
    {
        using var t = await PricedAsync();
        var booked = (await new ReservationService(t.Uow, Customer, new FakeEmailService())
            .CreateAsync(new CreateReservationRequest(FacilityId, SmallTypeId, MoveIn, 1), default)).Value!;
        await PublishAsync(t, FacilityId, 1_000_000m);
        var reservation = await t.Db.Reservations.SingleAsync(r => r.ReservationId == booked.ReservationId);
        reservation.StorageUnitId = A101;
        reservation.Status = ReservationStatus.Confirmed;
        t.Db.SaveChanges();

        var contract = await new ContractService(t.Uow, Staff, new AccessCodeService(t.Uow, new Pbkdf2PasswordHasher(), Staff, TestCodes.Protector))
            .CreateAsync(new CreateContractRequest(booked.ReservationId, 5), default);

        contract.IsSuccess.Should().BeTrue(contract.Error?.Message);
        contract.Value!.MonthlyRent.Should().Be(800_000m, "the customer was quoted Gò Vấp's version 1");
        (await RentCalculator.PriceAtAsync(t.Uow, (await t.Db.PricingPolicies.SingleAsync()).PricingPolicyId, FacilityId, SmallTypeId, VietnamTime.Today, default))!
            .BasePricePerMonth.Should().Be(1_000_000m, "new bookings get version 2");
    }

    [Fact]
    public async Task Publishing_supersedes_only_that_facilitys_version_and_live_versions_cannot_be_edited()
    {
        using var t = await PricedAsync();
        var prices = new FacilityPricingService(t.Uow, Ops);
        var first = (await prices.GetAsync(FacilityId, default)).Value!.Versions.Single();

        var draft = (await prices.CreateDraftAsync(FacilityId, default)).Value!;
        var second = await prices.CreateDraftAsync(FacilityId, default);
        var editLive = await prices.SetPriceAsync(FacilityId, first.FacilityPriceVersionId, SmallTypeId, Price(1), default);
        await prices.RemovePriceAsync(FacilityId, draft.FacilityPriceVersionId, SmallTypeId, default);
        await prices.PublishAsync(FacilityId, draft.FacilityPriceVersionId, default);
        var list = (await prices.GetAsync(FacilityId, default)).Value!;
        var policyId = (await t.Db.PricingPolicies.SingleAsync()).PricingPolicyId;

        draft.Prices.Should().ContainSingle(p => p.BasePricePerMonth == 800_000m, "a draft starts from the live version");
        second.Error!.Type.Should().Be(ErrorType.Conflict, "one draft at a time");
        editLive.Error!.Type.Should().Be(ErrorType.Conflict, "a live version is history");
        list.Versions.Select(v => (v.Version, v.Status)).Should().Equal((2, PricingPolicyStatus.Active), (1, PricingPolicyStatus.Superseded));
        list.Standard.Should().ContainSingle(p => p.BasePricePerMonth == 500_000m);
        (await RentCalculator.PriceAtAsync(t.Uow, policyId, FacilityId, SmallTypeId, VietnamTime.Today, default))!
            .BasePricePerMonth.Should().Be(500_000m, "version 2 prices nothing itself, so the standard applies again");
        (await new FacilityPricingService(t.Uow, new FakeCurrentUser(77, Roles.FacilityManager)).GetAsync(FacilityId, default))
            .Error!.Type.Should().Be(ErrorType.NotFound, "a manager reads only the facilities they manage");
    }

    [Fact]
    public async Task From_prices_are_the_lowest_anywhere_and_a_facility_sees_its_own()
    {
        using var t = await PricedAsync();
        await PublishAsync(t, OtherFacilityId, 300_000m);
        var policyId = (await t.Db.PricingPolicies.SingleAsync()).PricingPolicyId;

        (await RentCalculator.PricesAsync(t.Uow, policyId, null, VietnamTime.Today, default))[SmallTypeId].Should().Be(300_000m);
        (await RentCalculator.PricesAsync(t.Uow, policyId, FacilityId, VietnamTime.Today, default))[SmallTypeId].Should().Be(800_000m);
    }
}
