namespace StorEase.Application.Features.Reservations.Dtos;

public sealed record CreateReservationRequest(int FacilityId, int UnitTypeId, DateOnly MoveInDate, int RentalMonths);
