namespace StorEase.Domain.Entities;

/// <summary>Short lived, single use identity token for a customer visit check-in or check-out.</summary>
public sealed class UnitVisitPin
{
    public int UnitVisitPinId { get; set; }
    public int UserId { get; set; }
    public int ContractId { get; set; }
    public int? UnitVisitId { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiredAt { get; set; }
    public bool IsUsed { get; set; }

    public User User { get; set; } = null!;
    public Contract Contract { get; set; } = null!;
    public UnitVisit? UnitVisit { get; set; }
}
