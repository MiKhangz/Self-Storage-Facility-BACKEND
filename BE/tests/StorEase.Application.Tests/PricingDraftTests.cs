using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Pricing;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

/// <summary>Old prices come back as a new draft copied from their version; only drafts can be deleted.</summary>
public class PricingDraftTests
{
    [Fact]
    public async Task An_old_version_is_copied_into_one_new_draft_which_can_be_deleted_while_published_versions_stay()
    {
        using var t = Create();
        var old = new PricingPolicy
        {
            CreatedBy = StaffId, Version = 1, EffectiveFrom = new DateOnly(2025, 1, 1), EffectiveTo = VietnamTime.Today.AddDays(-1),
            DepositMonths = 1, GraceDays = 5, LateFeePercentPerWeek = 2, EarlyTerminationPercent = 20, SuspendAfterDays = 14,
            ClearOutAfterDays = 60, CancelFreeHours = 24, Status = PricingPolicyStatus.Superseded,
        };
        old.UnitPrices.Add(new UnitPrice { UnitTypeId = SmallTypeId, BasePricePerMonth = 900_000m, Discount3Month = 5, Discount6Month = 10, Discount12Month = 15 });
        old.FeeTypes.Add(new FeeType { Code = "SETUP", Name = "Setup", Amount = 50_000m });
        var active = new PricingPolicy
        {
            CreatedBy = StaffId, Version = 2, EffectiveFrom = VietnamTime.Today, DepositMonths = 2, Status = PricingPolicyStatus.Active,
        };
        t.Db.AddRange(old, active);
        t.Db.SaveChanges();
        var pricing = new PricingService(t.Uow, new FakeCurrentUser(StaffId, Roles.SystemAdministrator));

        var copy = await pricing.CopyToDraftAsync(old.PricingPolicyId, default);
        var second = await pricing.CopyToDraftAsync(active.PricingPolicyId, default);
        var deleteActive = await pricing.DeleteDraftAsync(active.PricingPolicyId, default);
        var deleted = await pricing.DeleteDraftAsync(copy.Value!.PricingPolicyId, default);

        copy.Value.Version.Should().Be(3);
        copy.Value.Status.Should().Be(PricingPolicyStatus.Draft);
        copy.Value.EffectiveFrom.Should().Be(VietnamTime.Today);
        copy.Value.EarlyTerminationPercent.Should().Be(20m);
        copy.Value.UnitPrices.Single().Should().Match<Features.Pricing.Dtos.UnitPriceDto>(p => p.BasePricePerMonth == 900_000m && p.Discount12Month == 15);
        second.Error!.Type.Should().Be(ErrorType.Conflict, "one draft at a time");
        deleteActive.Error!.Type.Should().Be(ErrorType.Conflict, "a published version is history");
        deleted.IsSuccess.Should().BeTrue();
        (await t.Db.PricingPolicies.CountAsync()).Should().Be(2);
        (await t.Db.FeeTypes.CountAsync()).Should().Be(1, "the copied fee went with the draft; the original stays");
        (await t.Db.PricingPolicies.FindAsync(old.PricingPolicyId))!.Status.Should().Be(PricingPolicyStatus.Superseded, "copying changes no history");
    }
}
