using FluentValidation;
using StorEase.Application.Features.Contracts.Dtos;

namespace StorEase.Application.Features.Contracts.Validators;

public class CreateContractRequestValidator : AbstractValidator<CreateContractRequest>
{
    public CreateContractRequestValidator()
    {
        RuleFor(x => x.ReservationId).GreaterThan(0);
        RuleFor(x => x.PaymentDay).InclusiveBetween(1, 28);
    }
}
