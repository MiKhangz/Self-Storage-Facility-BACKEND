using StorEase.Domain.Enums;

namespace StorEase.Application.Features.StorageUnits.Dtos;

/// <summary>MonthlyPrice is the active pricing policy's base rent for the size (null when none is set): the price
/// every unit of this size is rented at, before period discounts.</summary>
public sealed record UnitTypeDto(
    int UnitTypeId,
    string Code,
    string Name,
    decimal AreaM2,
    decimal Width,
    decimal Depth,
    decimal Height,
    bool IsClimateControlled,
    decimal? MonthlyPrice);

<<<<<<< Updated upstream
=======
/// <summary>One unit of the requested size at the facility, and whether it can be booked for the stay.</summary>
public sealed record UnitSlotDto(
    int StorageUnitId,
    string UnitCode,
    string FloorName,
    string? Zone,
    StorageUnitStatus Status,
    bool IsAvailable);

/// <summary>AvailableUnits counts units free for the whole stay (move-in date for RentalMonths); ExpectedUnitCode is the
/// unit a booking made now would hold, or null when none is free.</summary>
>>>>>>> Stashed changes
public sealed record UnitAvailabilityDto(
    int FacilityId,
    int UnitTypeId,
    DateOnly MoveInDate,
    int AvailableUnits,
<<<<<<< Updated upstream
    int TotalUnits);

public sealed record QuoteFeeDto(string Code, string Name, decimal Amount);
=======
    int TotalUnits,
    int RentalMonths = 1,
    string? ExpectedUnitCode = null,
    IReadOnlyList<UnitSlotDto>? Units = null);
>>>>>>> Stashed changes

public sealed record UnitPriceQuoteDto(
    int FacilityId,
    int UnitTypeId,
    DateOnly MoveInDate,
    int RentalMonths,
    int PricingPolicyId,
    int PricingPolicyVersion,
    decimal MonthlyRent,
    decimal RentSubtotal,
    decimal DepositAmount,
    IReadOnlyList<QuoteFeeDto> Fees,
    decimal TotalAmount);

public sealed record UnitTypeRequest(
    string Code,
    string Name,
    decimal AreaM2,
    decimal Width,
    decimal Depth,
    decimal Height,
    bool IsClimateControlled);
