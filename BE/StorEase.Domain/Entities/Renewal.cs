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

    public Contract Contract { get; set; } = null!;
    public User? ApprovedByUser { get; set; }
}
