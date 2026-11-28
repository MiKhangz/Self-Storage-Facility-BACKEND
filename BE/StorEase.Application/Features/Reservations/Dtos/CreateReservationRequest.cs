using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Reservations.Dtos;

public sealed record CreateReservationRequest(
    int FacilityId,
    int UnitTypeId,
    DateOnly MoveInDate,
    int RentalMonths,
    int? StorageUnitId = null,
    bool SameDay = false,
    UnitAssignmentMode UnitAssignmentMode = UnitAssignmentMode.AUTO,
    IReadOnlyList<string>? Preferences = null,
    string? PreferenceNote = null);

public sealed record UpdateReservationDatesRequest(DateOnly MoveInDate, int RentalMonths);

