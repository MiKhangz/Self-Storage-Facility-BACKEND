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

public sealed class GatewayCallbackRequestValidator : AbstractValidator<GatewayCallbackRequest>
{
    public GatewayCallbackRequestValidator()
    {
        RuleFor(x => x.PaymentCode).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Status).Must(s => s is Domain.Enums.PaymentStatus.Succeeded or Domain.Enums.PaymentStatus.Failed)
            .WithMessage("Status must be Succeeded or Failed.");
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.ReferenceNo).MaximumLength(50);
        RuleFor(x => x.Signature).NotEmpty().MaximumLength(128);
    }
}
