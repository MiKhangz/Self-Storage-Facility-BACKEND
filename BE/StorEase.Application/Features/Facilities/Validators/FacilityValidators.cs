using StorEase.Domain.Enums;
using FluentValidation;
using StorEase.Application.Features.Facilities.Dtos;

namespace StorEase.Application.Features.Facilities.Validators;

public class FacilityRequestValidator : AbstractValidator<FacilityRequest>
{
    public FacilityRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(10).Matches("^[A-Za-z0-9-]+$");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(200);
        RuleFor(x => x.District).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Phone).MaximumLength(20);
        RuleFor(x => x.OpeningHours).MaximumLength(50);
        RuleFor(x => x.ManagerId).GreaterThan(0).When(x => x.ManagerId.HasValue);
    }
}

public class FloorRequestValidator : AbstractValidator<FloorRequest>
{
    public FloorRequestValidator()
    {
        RuleFor(x => x.FloorNumber).InclusiveBetween(-5, 200);
        RuleFor(x => x.Name).MaximumLength(50);
        RuleFor(x => x.Status).IsInEnum().NotEqual(FloorStatus.Removed).WithMessage("Remove a floor with DELETE /api/floors/{id}.");
    }
}
