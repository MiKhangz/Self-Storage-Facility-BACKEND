using FluentValidation;
using StorEase.Application.Features.SupportTickets.Dtos;

namespace StorEase.Application.Features.SupportTickets.Validators;

public class AssignTicketRequestValidator : AbstractValidator<AssignTicketRequest>
{
    public AssignTicketRequestValidator()
    {
        RuleFor(x => x.AssigneeId).GreaterThan(0);
        RuleFor(x => x.DueDate)
            .Must(d => d!.Value.Date >= DateTime.UtcNow.Date)
            .When(x => x.DueDate is not null)
            .WithMessage("Due date cannot be in the past.");
    }
}

public class ChangeTicketStatusRequestValidator : AbstractValidator<ChangeTicketStatusRequest>
{
    public ChangeTicketStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
    }
}
