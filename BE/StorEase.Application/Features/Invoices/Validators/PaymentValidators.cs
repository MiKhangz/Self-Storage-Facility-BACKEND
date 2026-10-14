using FluentValidation;
using StorEase.Application.Features.Invoices.Dtos;

namespace StorEase.Application.Features.Invoices.Validators;

public sealed class PaymentRequestValidator : AbstractValidator<PaymentRequest>
{
    public PaymentRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).LessThanOrEqualTo(9999999999.99m);
        RuleFor(x => x.ReferenceNo).MaximumLength(50).When(x => x.ReferenceNo is not null);
    }
}
