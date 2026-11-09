using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Pricing.Dtos;

/// <summary>
/// A facility's price list: the company standard prices in force today (used for every size the facility does not
/// price itself) and the facility's own versions, newest first.
/// </summary>
public sealed record FacilityPriceListDto(
    int FacilityId, string FacilityName, int? StandardVersion, IReadOnlyList<FacilitySizePriceDto> Standard, IReadOnlyList<FacilityPriceVersionDto> Versions);

public sealed record FacilityPriceVersionDto(
    int FacilityPriceVersionId, int FacilityId, int Version, DateOnly EffectiveFrom, DateOnly? EffectiveTo, PricingPolicyStatus Status,
    IReadOnlyList<FacilitySizePriceDto> Prices, PriceVersionAuditDto? Audit = null);

/// <summary>A price version's history details: who drafted and who published it, and how many bookings were quoted on it.</summary>
public sealed record PriceVersionAuditDto(string? CreatedByName, string? PublishedByName, DateTime? PublishedAt, int Bookings);

public sealed record FacilitySizePriceDto(
    int UnitTypeId, string UnitTypeCode, string UnitTypeName, decimal BasePricePerMonth, decimal Discount3Month, decimal Discount6Month, decimal Discount12Month);
