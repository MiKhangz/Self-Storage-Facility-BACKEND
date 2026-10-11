using FluentValidation;
using StorEase.Application.Features.Maintenance.Dtos;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Maintenance.Validators;

public class CreateMaintenanceOrderRequestValidator : AbstractValidator<CreateMaintenanceOrderRequest>
{
    public CreateMaintenanceOrderRequestValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(300);
        RuleFor(x => x)
            .Must(x => x.StorageUnitId is > 0 || x.TicketId is > 0)
            .WithMessage("Give a storageUnitId or a ticketId.");
        RuleFor(x => x.AssignedTo).GreaterThan(0).When(x => x.AssignedTo is not null);
    }
}

public class ChangeMaintenanceStatusRequestValidator : AbstractValidator<ChangeMaintenanceStatusRequest>
{
    public ChangeMaintenanceStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.ScheduledAt).NotNull().When(x => x.Status == MaintenanceOrderStatus.Scheduled)
            .WithMessage("scheduledAt is required to schedule an order.");
        RuleFor(x => x.Cost).NotNull().GreaterThanOrEqualTo(0).LessThan(10_000_000_000m)
            .When(x => x.Status == MaintenanceOrderStatus.Done)
            .WithMessage("cost (>= 0) is required to complete an order.");
        RuleFor(x => x.WorkNote).NotEmpty().MaximumLength(500)
            .When(x => x.Status == MaintenanceOrderStatus.Done)
            .WithMessage("workNote is required to complete an order.");
    }
}
