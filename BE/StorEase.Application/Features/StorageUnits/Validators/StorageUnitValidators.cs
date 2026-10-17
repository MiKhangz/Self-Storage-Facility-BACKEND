using FluentValidation;
using StorEase.Application.Features.StorageUnits.Dtos;

namespace StorEase.Application.Features.StorageUnits.Validators;

public class CreateStorageUnitRequestValidator : AbstractValidator<CreateStorageUnitRequest>
{
    public CreateStorageUnitRequestValidator()
    {
        RuleFor(x => x.FacilityId).GreaterThan(0);
        RuleFor(x => x.FloorId).GreaterThan(0);
        RuleFor(x => x.UnitTypeId).GreaterThan(0);
        RuleFor(x => x.UnitCode).NotEmpty().MaximumLength(20).Matches("^[A-Za-z0-9-]+$");
        RuleFor(x => x.Zone).MaximumLength(10);
        RuleFor(x => x.CurrentPrice).GreaterThanOrEqualTo(0).LessThanOrEqualTo(9999999999.99m);
        RuleFor(x => x.Note).MaximumLength(200);
    }
}

public class UpdateStorageUnitRequestValidator : AbstractValidator<UpdateStorageUnitRequest>
{
    public UpdateStorageUnitRequestValidator()
    {
        RuleFor(x => x.FloorId).GreaterThan(0);
        RuleFor(x => x.UnitTypeId).GreaterThan(0);
        RuleFor(x => x.UnitCode).NotEmpty().MaximumLength(20).Matches("^[A-Za-z0-9-]+$");
        RuleFor(x => x.Zone).MaximumLength(10);
        RuleFor(x => x.CurrentPrice).GreaterThanOrEqualTo(0).LessThanOrEqualTo(9999999999.99m);
        RuleFor(x => x.Note).MaximumLength(200);
    }
}
