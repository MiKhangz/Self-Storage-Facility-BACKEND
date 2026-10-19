using FluentValidation;
using StorEase.Application.Features.CheckIns.Dtos;

namespace StorEase.Application.Features.CheckIns.Validators;

public class CreateCheckInRequestValidator : AbstractValidator<CreateCheckInRequest>
{
    public CreateCheckInRequestValidator()
    {
        RuleFor(x => x.ReservationId).GreaterThan(0);
        RuleFor(x => x.ContractId).GreaterThan(0);
        RuleFor(x => x.LockSerial).NotEmpty().MaximumLength(30);
        RuleFor(x => x.CardNumber).NotEmpty().MaximumLength(30);
        RuleFor(x => x.ConditionNote).MaximumLength(300);
    }
}
