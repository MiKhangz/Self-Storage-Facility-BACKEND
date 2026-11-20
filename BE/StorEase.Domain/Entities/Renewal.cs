namespace StorEase.Domain.Entities;

public class Renewal
{
    public int RenewalId { get; set; }
    public int ContractId { get; set; }
    public int? ApprovedBy { get; set; }
    public DateTime RequestedAt { get; set; }
    public int ExtendMonths { get; set; }
    public DateOnly NewEndDate { get; set; }
    public decimal NewMonthlyRent { get; set; }
    public string Status { get; set; } = string.Empty;
    /// <summary>Another unit for the renewed months, when the next customer has booked this one; the renter must agree.</summary>
    public int? TransferStorageUnitId { get; set; }

    public Contract Contract { get; set; } = null!;
    public User? ApprovedByUser { get; set; }
    public StorageUnit? TransferStorageUnit { get; set; }
}
