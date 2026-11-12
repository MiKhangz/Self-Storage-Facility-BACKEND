using StorEase.Domain.Enums;

namespace StorEase.Domain.Entities;

/// <summary>
/// One version of a facility's own price list: Draft, then Active when published, then Superseded by the next.
/// It prices some sizes; every other size, and all rental rules, come from the company standard (PricingPolicy).
/// </summary>
public class FacilityPriceVersion
{
    public int FacilityPriceVersionId { get; set; }
    public int FacilityId { get; set; }
    /// <summary>Counts per facility: 1, 2, 3…</summary>
    public int Version { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public PricingPolicyStatus Status { get; set; }
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    public Facility Facility { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public ICollection<FacilityUnitPrice> Prices { get; set; } = [];
}

/// <summary>A facility's own monthly price and long-stay discounts for one size, in one version.</summary>
public class FacilityUnitPrice
{
    public int FacilityUnitPriceId { get; set; }
    public int FacilityPriceVersionId { get; set; }
    public int UnitTypeId { get; set; }
    public decimal BasePricePerMonth { get; set; }
    public decimal Discount3Month { get; set; }
    public decimal Discount6Month { get; set; }
    public decimal Discount12Month { get; set; }

    public FacilityPriceVersion FacilityPriceVersion { get; set; } = null!;
    public UnitType UnitType { get; set; } = null!;
}
