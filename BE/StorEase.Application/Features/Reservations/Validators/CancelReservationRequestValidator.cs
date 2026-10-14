using FluentValidation;
using StorEase.Application.Features.Reservations.Dtos;

namespace StorEase.Application.Features.Reservations.Validators;

public class CancelReservationRequestValidator : AbstractValidator<CancelReservationRequest>
{
    public CancelReservationRequestValidator() => RuleFor(x => x.Reason).MaximumLength(200);
}
