using FluentValidation;
using StorEase.Application.Features.Handovers.Dtos;

namespace StorEase.Application.Features.Handovers.Validators;

public class CreateHandoverRequestValidator : AbstractValidator<CreateHandoverRequest>
{
    public CreateHandoverRequestValidator()
    {
        RuleFor(x => x.ReservationId).GreaterThan(0);
        RuleFor(x => x.ContractId).GreaterThan(0);
        RuleFor(x => x.LockSerial).NotEmpty().MaximumLength(30);
        // Facilities hand over keys now, not access cards; an older client may still send a card number.
        RuleFor(x => x.CardNumber).MaximumLength(30);
        RuleFor(x => x.ConditionNote).MaximumLength(300);
    }
}
