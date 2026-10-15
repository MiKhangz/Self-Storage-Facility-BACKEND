using FluentAssertions;
using StorEase.Application.Common;
using StorEase.Application.Features.StorageUnits;
using StorEase.Application.Features.StorageUnits.Dtos;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

public class StorageUnitPricingTests
{
    [Fact]
    public async Task A_unit_without_a_price_takes_the_policy_price_for_its_size_and_follows_a_size_change()
    {
        using var t = Create();
        var policy = new PricingPolicy { CreatedBy = StaffId, Version = 1, EffectiveFrom = new DateOnly(2025, 1, 1), Status = PricingPolicyStatus.Active };
        t.Db.AddRange(
            new UnitPrice { PricingPolicy = policy, UnitTypeId = SmallTypeId, BasePricePerMonth = 900_000m },
            new UnitPrice { PricingPolicy = policy, UnitTypeId = LargeTypeId, BasePricePerMonth = 1_600_000m });
        t.Db.SaveChanges();
        var units = new StorageUnitService(t.Uow, new FakeCurrentUser(StaffId, Roles.SystemAdministrator));

        var created = await units.CreateAsync(new CreateStorageUnitRequest(FacilityId, 1, SmallTypeId, "B-201", null, null, null), default);
        var id = created.Value!.StorageUnitId;
        var renamed = await units.UpdateAsync(id, new UpdateStorageUnitRequest(1, SmallTypeId, "B-202", null, null, null), default);
        var resized = await units.UpdateAsync(id, new UpdateStorageUnitRequest(1, LargeTypeId, "B-202", null, null, null), default);

        created.Value.CurrentPrice.Should().Be(900_000m);
        renamed.Value!.CurrentPrice.Should().Be(900_000m, "no price and the same size keeps the price");
        resized.Value!.CurrentPrice.Should().Be(1_600_000m, "a new size takes that size's policy price");
    }

    [Fact]
    public async Task The_size_list_carries_the_active_policy_price_so_staff_see_what_customers_pay()
    {
        using var t = Create();
        var catalog = new UnitCatalogService(t.Uow);
        var withoutPolicy = await catalog.ListUnitTypesAsync(default);

        t.Db.Add(new UnitPrice
        {
            PricingPolicy = new PricingPolicy { CreatedBy = StaffId, Version = 1, EffectiveFrom = new DateOnly(2025, 1, 1), Status = PricingPolicyStatus.Active },
            UnitTypeId = SmallTypeId, BasePricePerMonth = 900_000m,
        });
        t.Db.SaveChanges();
        var withPolicy = await catalog.ListUnitTypesAsync(default);

        withoutPolicy.Value!.Should().OnlyContain(u => u.MonthlyPrice == null);
        withPolicy.Value!.Single(u => u.UnitTypeId == SmallTypeId).MonthlyPrice.Should().Be(900_000m);
        withPolicy.Value!.Single(u => u.UnitTypeId == LargeTypeId).MonthlyPrice.Should().BeNull("the policy has no price for it");
    }
}
