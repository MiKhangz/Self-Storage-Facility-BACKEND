namespace StorEase.Application.Features.Reservations.Dtos;

/// <param name="HandoverAt">The handover appointment the customer picks with the booking (Vietnam time); it counts once
/// the deposit is paid. Left out, the move-in day is the deadline.</param>
public sealed record CreateReservationRequest(int FacilityId, int UnitTypeId, DateOnly MoveInDate, int RentalMonths, DateTime? HandoverAt = null);
