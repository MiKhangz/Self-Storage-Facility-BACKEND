using FluentValidation;
using StorEase.Application.Features.SupportTickets.Dtos;

namespace StorEase.Application.Features.SupportTickets.Validators;

public class CreateTicketRequestValidator : AbstractValidator<CreateTicketRequest>
{
    public CreateTicketRequestValidator()
    {
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200);
        // The description becomes the first message, which is nvarchar(1000).
        RuleFor(x => x.Description).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.StorageUnitId).GreaterThan(0).When(x => x.StorageUnitId is not null);
        RuleFor(x => x.FacilityId).GreaterThan(0).When(x => x.FacilityId is not null);
        RuleFor(x => x)
            .Must(x => x.StorageUnitId is not null || x.FacilityId is not null)
            .WithMessage("Provide a storage unit or a facility.");
        RuleFor(x => x.Priority).IsInEnum().When(x => x.Priority is not null);
    }
}
