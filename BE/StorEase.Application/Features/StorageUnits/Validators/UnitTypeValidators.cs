using FluentValidation;
using StorEase.Application.Features.StorageUnits.Dtos;

namespace StorEase.Application.Features.StorageUnits.Validators;

public sealed class UnitTypeRequestValidator : AbstractValidator<UnitTypeRequest>
{
    public UnitTypeRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.AreaM2).GreaterThan(0).LessThanOrEqualTo(999.99m);
        RuleFor(x => x.Width).GreaterThan(0).LessThanOrEqualTo(99.99m);
        RuleFor(x => x.Depth).GreaterThan(0).LessThanOrEqualTo(99.99m);
        RuleFor(x => x.Height).GreaterThan(0).LessThanOrEqualTo(99.99m);
    }
}
