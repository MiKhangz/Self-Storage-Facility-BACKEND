using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Reservations.Dtos;

/// <summary>
/// Used for both the single reservation and every item of the reservation list. The deposit fields describe the
/// deposit invoice raised when the reservation was made; they are null when no deposit was due.
/// </summary>
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
    int? ContractId,
    int? DepositInvoiceId = null,
    decimal? DepositAmount = null,
    InvoiceStatus? DepositStatus = null);

public sealed record CancelReservationRequest(string? Reason);

/// <summary>
/// <see cref="DepositRefundable"/> is true when a paid deposit should be returned (cancelled inside the policy's
/// free-cancellation window, or by staff); the refund itself is recorded by Business Operations.
/// </summary>
public sealed record ReservationCancellationDto(ReservationDto Reservation, decimal DepositPaid, bool DepositRefundable);

public sealed record CheckInItemDto(string Key, string Label, bool Done, string? Detail);

/// <summary>What staff confirm at the counter before handover. Handover is allowed when <see cref="ReadyForHandover"/>.</summary>
public sealed record CheckInChecklistDto(
    int ReservationId,
    string ReservationCode,
    string CustomerName,
    string CustomerPhone,
    string? CitizenId,
    string? UnitCode,
    int? ContractId,
    IReadOnlyList<CheckInItemDto> Items,
    bool ReadyForHandover);
