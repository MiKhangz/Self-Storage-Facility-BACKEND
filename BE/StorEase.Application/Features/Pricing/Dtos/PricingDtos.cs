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

/// <summary>The rules customers agree to when booking, from the policy in force today. Public: shown before sign-in.</summary>
public sealed record PolicyTermsDto(
    int PricingPolicyId, int Version, int DepositMonths, int GraceDays, int NoticeDays, decimal LateFeePercentPerWeek,
    int SuspendAfterDays, int ClearOutAfterDays, int CancelFreeHours, IReadOnlyList<SizeDiscountDto> Discounts);

/// <summary>Percent off the monthly rent for a stay of at least 3, 6 or 12 months.</summary>
public sealed record SizeDiscountDto(int UnitTypeId, decimal Discount3Month, decimal Discount6Month, decimal Discount12Month);

