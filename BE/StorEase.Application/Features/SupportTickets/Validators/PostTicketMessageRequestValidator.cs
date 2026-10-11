using FluentValidation;
using StorEase.Application.Features.SupportTickets.Dtos;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.SupportTickets.Validators;

public class PostTicketMessageRequestValidator : AbstractValidator<PostTicketMessageRequest>
{
    public PostTicketMessageRequestValidator()
    {
        RuleFor(x => x.Body).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.NextStatus)
            .Must(s => s is SupportTicketStatus.InProgress or SupportTicketStatus.WaitingForCustomer)
            .When(x => x.NextStatus is not null)
            .WithMessage("A reply can only set the status to InProgress or WaitingForCustomer.");
    }
}
