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
    public int SuspendAfterDays { get; set; }
    public int ClearOutAfterDays { get; set; }
    public int CancelFreeHours { get; set; }
    public PricingPolicyStatus Status { get; set; }

    public User CreatedByUser { get; set; } = null!;
    public ICollection<UnitPrice> UnitPrices { get; set; } = [];
    public ICollection<FeeType> FeeTypes { get; set; } = [];
}
