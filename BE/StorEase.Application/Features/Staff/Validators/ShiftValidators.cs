using FluentValidation;
using StorEase.Application.Features.Staff.Dtos;

namespace StorEase.Application.Features.Staff.Validators;

public class CreateShiftRequestValidator : AbstractValidator<CreateShiftRequest>
{
    public CreateShiftRequestValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.FacilityId).GreaterThan(0);
        RuleFor(x => x.ShiftType).IsInEnum();
        RuleFor(x => x.EndTime).NotEqual(x => x.StartTime)
            .WithMessage("A shift cannot start and end at the same time.");
    }
}

public class UpdateShiftRequestValidator : AbstractValidator<UpdateShiftRequest>
{
    public UpdateShiftRequestValidator()
    {
        RuleFor(x => x.ShiftType).IsInEnum();
        RuleFor(x => x.EndTime).NotEqual(x => x.StartTime)
            .WithMessage("A shift cannot start and end at the same time.");
    }
}
