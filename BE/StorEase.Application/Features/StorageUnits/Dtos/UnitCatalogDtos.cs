using StorEase.Domain.Enums;

namespace StorEase.Application.Features.StorageUnits.Dtos;

public sealed record UnitTypeDto(
    int UnitTypeId,
    string Code,
    string Name,
    decimal AreaM2,
    decimal Width,
    decimal Depth,
    decimal Height,
    bool IsClimateControlled);

public sealed record UnitAvailabilityDto(
    int FacilityId,
    int UnitTypeId,
    DateOnly MoveInDate,
    int AvailableUnits,
    int TotalUnits);

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
    decimal TotalAmount);

public sealed record CreateUnitTypeRequest(
    string Code,
    string Name,
    decimal AreaM2,
    decimal Width,
    decimal Depth,
    decimal Height,
    bool IsClimateControlled);

public sealed record UpdateUnitTypeRequest(
    string Code,
    string Name,
    decimal AreaM2,
    decimal Width,
    decimal Depth,
    decimal Height,
    bool IsClimateControlled);
