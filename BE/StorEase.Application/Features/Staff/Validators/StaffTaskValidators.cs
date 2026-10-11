using FluentValidation;
using StorEase.Application.Features.Staff.Dtos;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Staff.Validators;

public class CreateStaffTaskRequestValidator : AbstractValidator<CreateStaffTaskRequest>
{
    public CreateStaffTaskRequestValidator()
    {
        RuleFor(x => x.ShiftId).GreaterThan(0);
        RuleFor(x => x.AssignedTo).GreaterThan(0);
        RuleFor(x => x.TaskType).IsInEnum();
        RuleFor(x => x.ReferenceCode).MaximumLength(30);
        RuleFor(x => x.Note).MaximumLength(200);
        RuleFor(x => x.Note).NotEmpty().When(x => x.TaskType == TaskType.Other)
            .WithMessage("note is required for a task of type Other.");
    }
}

public class ChangeStaffTaskStatusRequestValidator : AbstractValidator<ChangeStaffTaskStatusRequest>
{
    public ChangeStaffTaskStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
    }
}
