using FluentValidation;
using StorEase.Application.Features.Pricing.Dtos;

namespace StorEase.Application.Features.Pricing.Validators;

public sealed class CreatePricingPolicyRequestValidator : AbstractValidator<CreatePricingPolicyRequest>
{
    public CreatePricingPolicyRequestValidator()
    {
        RuleFor(x => x.EffectiveFrom).NotEmpty();
        RuleFor(x => x.EffectiveTo).GreaterThanOrEqualTo(x => x.EffectiveFrom).When(x => x.EffectiveTo.HasValue);
        RuleFor(x => x.DepositMonths).InclusiveBetween(0, 12);
        RuleFor(x => x.GraceDays).InclusiveBetween(0, 90);
        RuleFor(x => x.NoticeDays).InclusiveBetween(0, 365);
        RuleFor(x => x.LateFeePercentPerWeek).InclusiveBetween(0, 100);
        RuleFor(x => x.EarlyTerminationPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.SuspendAfterDays).InclusiveBetween(0, 365);
        RuleFor(x => x.ClearOutAfterDays).InclusiveBetween(0, 365);
        RuleFor(x => x.CancelFreeHours).InclusiveBetween(0, 720);
    }
}

public sealed class UpsertUnitPriceRequestValidator : AbstractValidator<UpsertUnitPriceRequest>
{
    public UpsertUnitPriceRequestValidator()
    {
        RuleFor(x => x.BasePricePerMonth).GreaterThanOrEqualTo(0).LessThanOrEqualTo(9999999999.99m);
        RuleFor(x => x.Discount3Month).InclusiveBetween(0, 100);
        RuleFor(x => x.Discount6Month).InclusiveBetween(0, 100);
        RuleFor(x => x.Discount12Month).InclusiveBetween(0, 100);
    }
}

public sealed class FeeTypeRequestValidator : AbstractValidator<FeeTypeRequest>
{
    public FeeTypeRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20).Matches("^[A-Za-z0-9_-]+$");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0).LessThanOrEqualTo(9999999999.99m);
        RuleFor(x => x.Amount).LessThanOrEqualTo(100).When(x => x.IsPercentage).WithMessage("A percentage fee cannot exceed 100.");
    }
}
