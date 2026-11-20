using StorEase.Domain.Enums;

namespace StorEase.Domain.Entities;

public class PricingPolicy
{
    public int PricingPolicyId { get; set; }
    public int CreatedBy { get; set; }
    public int Version { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public int DepositMonths { get; set; }
    public int GraceDays { get; set; }
    public int NoticeDays { get; set; } = 30;
    public decimal LateFeePercentPerWeek { get; set; }
    /// <summary>Percent of the deposit kept when the unit is handed back before the contract's end date (SR-04).</summary>
    public decimal EarlyTerminationPercent { get; set; }
    public int SuspendAfterDays { get; set; }
    public int ClearOutAfterDays { get; set; }
    public int CancelFreeHours { get; set; }
    /// <summary>Days before a rental's end date when the customer is reminded to renew or hand back, and staff call them.</summary>
    public int EndReminderDays { get; set; } = 10;
    /// <summary>Daily fee while a renter stays past the end date, as a percent of one day's rent.</summary>
    public decimal OverstayFeePercent { get; set; } = 150;
    /// <summary>Days of overstay after which staff may move the goods to the central warehouse and free the unit.</summary>
    public int MoveOutAfterDays { get; set; } = 7;
    public PricingPolicyStatus Status { get; set; }

    public User CreatedByUser { get; set; } = null!;
    public ICollection<UnitPrice> UnitPrices { get; set; } = [];
    public ICollection<FeeType> FeeTypes { get; set; } = [];
}
