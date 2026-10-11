using FluentValidation;
using StorEase.Application.Features.Reservations.Dtos;

namespace StorEase.Application.Features.Reservations.Validators;

public class CreateReservationRequestValidator : AbstractValidator<CreateReservationRequest>
{
    public const int MaxRentalMonths = 36;

    public CreateReservationRequestValidator()
    {
        RuleFor(x => x.FacilityId).GreaterThan(0);
        RuleFor(x => x.UnitTypeId).GreaterThan(0);
        RuleFor(x => x.MoveInDate)
            .GreaterThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Move-in date cannot be in the past.");
        RuleFor(x => x.RentalMonths).InclusiveBetween(1, MaxRentalMonths);
    }
}
