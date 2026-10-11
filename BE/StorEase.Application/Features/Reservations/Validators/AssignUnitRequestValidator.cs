using FluentValidation;
using StorEase.Application.Features.Reservations.Dtos;

namespace StorEase.Application.Features.Reservations.Validators;

public class AssignUnitRequestValidator : AbstractValidator<AssignUnitRequest>
{
    public AssignUnitRequestValidator()
    {
        RuleFor(x => x.StorageUnitId).GreaterThan(0);
    }
}
