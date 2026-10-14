using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Pricing.Dtos;

public sealed record PricingPolicyDto(
    int PricingPolicyId,
    int Version,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    int DepositMonths,
    int GraceDays,
    int NoticeDays,
    decimal LateFeePercentPerWeek,
    int SuspendAfterDays,
    int ClearOutAfterDays,
    int CancelFreeHours,
    PricingPolicyStatus Status,
    IReadOnlyList<UnitPriceDto> UnitPrices,
    IReadOnlyList<FeeTypeDto> Fees);

public sealed record UnitPriceDto(
    int UnitPriceId,
    int PricingPolicyId,
    int UnitTypeId,
    string UnitTypeCode,
    string UnitTypeName,
    decimal BasePricePerMonth,
    decimal Discount3Month,
    decimal Discount6Month,
    decimal Discount12Month);

public sealed record FeeTypeDto(int FeeTypeId, int PricingPolicyId, string Code, string Name, decimal Amount, bool IsPercentage);

public sealed record CreatePricingPolicyRequest(
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    int DepositMonths,
    int GraceDays,
    int NoticeDays,
    decimal LateFeePercentPerWeek,
    int SuspendAfterDays,
    int ClearOutAfterDays,
    int CancelFreeHours);

public sealed record UpsertUnitPriceRequest(
    decimal BasePricePerMonth,
    decimal Discount3Month,
    decimal Discount6Month,
    decimal Discount12Month);

public sealed record FeeTypeRequest(string Code, string Name, decimal Amount, bool IsPercentage);
