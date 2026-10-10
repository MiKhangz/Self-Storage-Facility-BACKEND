namespace StorEase.Domain.Entities;

public class FeeType
{
    public int FeeTypeId { get; set; }
    public int PricingPolicyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public bool IsPercentage { get; set; }

    public PricingPolicy PricingPolicy { get; set; } = null!;
}
