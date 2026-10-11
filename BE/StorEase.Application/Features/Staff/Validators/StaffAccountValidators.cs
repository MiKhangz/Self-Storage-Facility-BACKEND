using FluentValidation;
using StorEase.Application.Common;
using StorEase.Application.Features.Staff.Dtos;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Staff.Validators;

public static class StaffAccountRules
{
    public static bool IsStaffRole(string? role) =>
        role is Roles.FacilityStaff or Roles.FacilityManager
            or Roles.BusinessOperationsManager or Roles.SystemAdministrator;
}

public class CreateStaffAccountRequestValidator : AbstractValidator<CreateStaffAccountRequest>
{
    public CreateStaffAccountRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().MaximumLength(100).EmailAddress();
        RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(100);
        RuleFor(x => x.Role).Must(StaffAccountRules.IsStaffRole)
            .WithMessage("Role must be FacilityStaff, FacilityManager, BusinessOperationsManager or SystemAdministrator.");
    }
}

public class UpdateStaffAccountRequestValidator : AbstractValidator<UpdateStaffAccountRequest>
{
    public UpdateStaffAccountRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100).When(x => x.FullName is not null);
        RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(20).When(x => x.PhoneNumber is not null);
        RuleFor(x => x.Role).Must(StaffAccountRules.IsStaffRole).When(x => x.Role is not null)
            .WithMessage("Role must be a staff role.");
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
        RuleFor(x => x.Status).NotEqual(UserStatus.Pending).When(x => x.Status is not null)
            .WithMessage("Status cannot be set back to Pending.");
    }
}

public class SetStaffFacilitiesRequestValidator : AbstractValidator<SetStaffFacilitiesRequest>
{
    public SetStaffFacilitiesRequestValidator()
    {
        RuleFor(x => x.FacilityIds).NotNull();
        RuleForEach(x => x.FacilityIds).GreaterThan(0);
    }
}
