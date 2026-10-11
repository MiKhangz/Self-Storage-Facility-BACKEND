using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Reservations.Dtos;

/// <summary>Used for both the single reservation and every item of the reservation list.</summary>
public sealed record ReservationDto(
    int ReservationId,
    string ReservationCode,
    int CustomerId,
    string CustomerName,
    int FacilityId,
    string FacilityName,
    int UnitTypeId,
    string UnitTypeName,
    int? StorageUnitId,
    string? UnitCode,
    DateOnly MoveInDate,
    int RentalMonths,
    ReservationStatus Status,
    DateTime CreatedAt,
    int? ContractId);
