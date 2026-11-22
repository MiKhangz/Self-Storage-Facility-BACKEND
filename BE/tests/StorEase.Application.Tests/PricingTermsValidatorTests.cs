using FluentAssertions;
using StorEase.Application.Features.Pricing.Dtos;
using StorEase.Application.Features.Pricing.Validators;

namespace StorEase.Application.Tests;

/// <summary>Creating and editing a price version accept exactly the same terms, with the same messages.</summary>
public class PricingTermsValidatorTests
{
    [Theory]
    [InlineData(null, 0)]
    [InlineData("EffectiveFrom", 1)]
    [InlineData("EffectiveTo", 1)]
    [InlineData("DepositMonths", 1)]
    [InlineData("GraceDays", 1)]
    [InlineData("NoticeDays", 1)]
    [InlineData("LateFeePercentPerWeek", 1)]
    [InlineData("EarlyTerminationPercent", 1)]
    [InlineData("SuspendAfterDays", 1)]
    [InlineData("ClearOutAfterDays", 1)]
    [InlineData("CancelFreeHours", 1)]
    [InlineData("EndReminderDays", 1)]
    [InlineData("OverstayFeePercent", 1)]
    [InlineData("MoveOutAfterDays", 1)]
    [InlineData("CentralStorageKeepDays", 1)]
    public void Each_term_is_checked_on_create_and_on_update(string? broken, int errors)
    {
        int Of(string name, int good) => broken == name ? 9999 : good;
        var from = broken == "EffectiveFrom" ? default : new DateOnly(2026, 12, 1);
        DateOnly? to = broken == "EffectiveTo" ? from.AddDays(-1) : null;
        var create = new CreatePricingPolicyRequest(from, to, Of("DepositMonths", 2), Of("GraceDays", 5), Of("NoticeDays", 30),
            Of("LateFeePercentPerWeek", 2), Of("EarlyTerminationPercent", 20), Of("SuspendAfterDays", 8), Of("ClearOutAfterDays", 13),
            Of("CancelFreeHours", 24), Of("EndReminderDays", 10), Of("OverstayFeePercent", 150), Of("MoveOutAfterDays", 7),
            Of("CentralStorageKeepDays", 90));
        var update = new UpdatePricingPolicyRequest(from, to, create.DepositMonths, create.GraceDays, create.NoticeDays,
            create.LateFeePercentPerWeek, create.EarlyTerminationPercent, create.SuspendAfterDays, create.ClearOutAfterDays,
            create.CancelFreeHours, create.EndReminderDays, create.OverstayFeePercent, create.MoveOutAfterDays, create.CentralStorageKeepDays);

        var onCreate = new CreatePricingPolicyRequestValidator().Validate(create).Errors;
        var onUpdate = new UpdatePricingPolicyRequestValidator().Validate(update).Errors;

        onCreate.Should().HaveCount(errors);
        if (broken is not null)
            onCreate.Single().PropertyName.Should().Be(broken);
        onUpdate.Select(e => (e.PropertyName, e.ErrorMessage)).Should().Equal(onCreate.Select(e => (e.PropertyName, e.ErrorMessage)));
    }
}
