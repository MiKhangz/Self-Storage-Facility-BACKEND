using FluentValidation;
using StorEase.Application.Features.Invoices.Dtos;

namespace StorEase.Application.Features.Invoices.Validators;

public sealed class InvoiceAdjustmentRequestValidator : AbstractValidator<InvoiceAdjustmentRequest>
{
    public InvoiceAdjustmentRequestValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Amount).NotEqual(0).InclusiveBetween(-9999999999.99m, 9999999999.99m);
    }
}

public sealed class RefundRequestValidator : AbstractValidator<RefundRequest>
{
    public RefundRequestValidator() => RuleFor(x => x.Reason).MaximumLength(200);
}
