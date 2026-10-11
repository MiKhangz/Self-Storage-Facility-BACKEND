using FluentValidation;
using StorEase.Application.Features.Renewals.Dtos;

namespace StorEase.Application.Features.Renewals.Validators;

public class CreateRenewalRequestValidator : AbstractValidator<CreateRenewalRequest>
{
    public CreateRenewalRequestValidator()
    {
        RuleFor(x => x.ContractId).GreaterThan(0);
        RuleFor(x => x.ExtendMonths).InclusiveBetween(1, 36);
    }
}
