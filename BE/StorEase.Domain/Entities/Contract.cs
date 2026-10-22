using StorEase.Domain.Enums;

namespace StorEase.Domain.Entities;

public class Contract
{
    public int ContractId { get; set; }
    public int ReservationId { get; set; }
    public int CustomerId { get; set; }
    public int StorageUnitId { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal MonthlyRent { get; set; }
    /// <summary>Copied from the reservation: the terms (late fee, grace, early-return penalty) this contract was signed under.</summary>
    public int? PricingPolicyId { get; set; }
    public decimal DepositAmount { get; set; }
    public int PaymentDay { get; set; }
    public DateTime? SignedAt { get; set; }
    public ContractStatus Status { get; set; }

    public Reservation Reservation { get; set; } = null!;
    public PricingPolicy? PricingPolicy { get; set; }
    public User Customer { get; set; } = null!;
    public StorageUnit StorageUnit { get; set; } = null!;
    public ICollection<Invoice> Invoices { get; set; } = [];
    public ICollection<AccessCode> AccessCodes { get; set; } = [];
    public ICollection<Renewal> Renewals { get; set; } = [];
}
