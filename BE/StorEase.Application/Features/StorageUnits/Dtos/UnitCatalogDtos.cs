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

/// <summary>AvailableUnits counts units free for the whole stay (move-in date for RentalMonths); ExpectedUnitCode is the
/// unit a booking made now would hold, or null when none is free.</summary>
public sealed record UnitAvailabilityDto(
    int FacilityId,
    int UnitTypeId,
    DateOnly MoveInDate,
    int AvailableUnits,
    int TotalUnits,
    int RentalMonths = 1,
    string? ExpectedUnitCode = null);

public sealed record QuoteFeeDto(string Code, string Name, decimal Amount);

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
    decimal TotalAmount,
    string? ExpectedUnitCode = null);

public sealed record UnitTypeRequest(
    string Code,
    string Name,
    decimal AreaM2,
    decimal Width,
    decimal Depth,
    decimal Height,
    bool IsClimateControlled);
