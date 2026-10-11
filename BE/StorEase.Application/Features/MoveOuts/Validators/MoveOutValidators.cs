using FluentValidation;
using StorEase.Application.Features.MoveOuts.Dtos;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.MoveOuts.Validators;

public class CreateMoveOutRequestValidator : AbstractValidator<CreateMoveOutRequest>
{
    public CreateMoveOutRequestValidator()
    {
        RuleFor(x => x.ContractId).GreaterThan(0);
        RuleFor(x => x.Reason).MaximumLength(200);
        RuleFor(x => x.RefundMethod).IsInEnum();
        RuleFor(x => x.RefundAccount).MaximumLength(50);
        RuleFor(x => x.RefundAccount).NotEmpty()
            .When(x => x.RefundMethod == RefundMethod.BankTransfer)
            .WithMessage("A refund account is required for a bank transfer.");
    }
}

public class ApproveMoveOutRequestValidator : AbstractValidator<ApproveMoveOutRequest>
{
    public ApproveMoveOutRequestValidator()
    {
        RuleFor(x => x.InspectionSlot).Must(s => s > DateTime.UtcNow)
            .WithMessage("The inspection slot must be in the future.");
    }
}

public class CreateReturnInspectionRequestValidator : AbstractValidator<CreateReturnInspectionRequest>
{
    public CreateReturnInspectionRequestValidator()
    {
        RuleFor(x => x.ConditionResult).IsInEnum();
        RuleFor(x => x.CleaningFee).InclusiveBetween(0, 9_999_999_999.99m);
        RuleFor(x => x.RepairCharge).InclusiveBetween(0, 9_999_999_999.99m);
        RuleFor(x => x.OtherDeduction).InclusiveBetween(0, 9_999_999_999.99m);
    }
}
