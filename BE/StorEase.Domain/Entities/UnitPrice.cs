namespace StorEase.Domain.Entities;

public class UnitPrice
{
    public int UnitPriceId { get; set; }
    public int PricingPolicyId { get; set; }
    public int UnitTypeId { get; set; }
    public decimal BasePricePerMonth { get; set; }
    public decimal Discount3Month { get; set; }
    public decimal Discount6Month { get; set; }
    public decimal Discount12Month { get; set; }

    public PricingPolicy PricingPolicy { get; set; } = null!;
    public UnitType UnitType { get; set; } = null!;
}
