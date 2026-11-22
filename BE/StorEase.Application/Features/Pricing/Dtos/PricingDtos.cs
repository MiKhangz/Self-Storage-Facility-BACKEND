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
    decimal EarlyTerminationPercent,
    int SuspendAfterDays,
    int ClearOutAfterDays,
    int CancelFreeHours,
    PricingPolicyStatus Status,
    IReadOnlyList<UnitPriceDto> UnitPrices,
    IReadOnlyList<FeeTypeDto> Fees,
    PriceVersionAuditDto? Audit = null,
    int EndReminderDays = 10,
    decimal OverstayFeePercent = 150,
    int MoveOutAfterDays = 7,
    int CentralStorageKeepDays = 90);

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

/// <summary>The terms of a price version, checked the same when it is created and when it is edited.</summary>
public interface IPricingPolicyTerms
{
    DateOnly EffectiveFrom { get; }
    DateOnly? EffectiveTo { get; }
    int DepositMonths { get; }
    int GraceDays { get; }
    int NoticeDays { get; }
    decimal LateFeePercentPerWeek { get; }
    decimal EarlyTerminationPercent { get; }
    int SuspendAfterDays { get; }
    int ClearOutAfterDays { get; }
    int CancelFreeHours { get; }
    int EndReminderDays { get; }
    decimal OverstayFeePercent { get; }
    int MoveOutAfterDays { get; }
    int CentralStorageKeepDays { get; }
}

public sealed record CreatePricingPolicyRequest(
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    int DepositMonths,
    int GraceDays,
    int NoticeDays,
    decimal LateFeePercentPerWeek,
    decimal EarlyTerminationPercent,
    int SuspendAfterDays,
    int ClearOutAfterDays,
    int CancelFreeHours,
    int EndReminderDays = 10,
    decimal OverstayFeePercent = 150,
    int MoveOutAfterDays = 7,
    int CentralStorageKeepDays = 90) : IPricingPolicyTerms;

public sealed record UpdatePricingPolicyRequest(
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    int DepositMonths,
    int GraceDays,
    int NoticeDays,
    decimal LateFeePercentPerWeek,
    decimal EarlyTerminationPercent,
    int SuspendAfterDays,
    int ClearOutAfterDays,
    int CancelFreeHours,
    int EndReminderDays = 10,
    decimal OverstayFeePercent = 150,
    int MoveOutAfterDays = 7,
    int CentralStorageKeepDays = 90) : IPricingPolicyTerms;

public sealed record UpsertUnitPriceRequest(
    decimal BasePricePerMonth,
    decimal Discount3Month,
    decimal Discount6Month,
    decimal Discount12Month);

public sealed record FeeTypeRequest(string Code, string Name, decimal Amount, bool IsPercentage);

/// <summary>The rules customers agree to when booking, from the policy in force today. Public: shown before sign-in.</summary>
public sealed record PolicyTermsDto(
    int PricingPolicyId, int Version, int DepositMonths, int GraceDays, decimal LateFeePercentPerWeek, decimal EarlyTerminationPercent,
    int SuspendAfterDays, int ClearOutAfterDays, int CancelFreeHours, IReadOnlyList<SizeDiscountDto> Discounts,
    int EndReminderDays = 10, decimal OverstayFeePercent = 150, int MoveOutAfterDays = 7,
    int CentralStorageKeepDays = 90);

/// <summary>Percent off the monthly rent for a stay of at least 3, 6 or 12 months.</summary>
public sealed record SizeDiscountDto(int UnitTypeId, decimal Discount3Month, decimal Discount6Month, decimal Discount12Month);

