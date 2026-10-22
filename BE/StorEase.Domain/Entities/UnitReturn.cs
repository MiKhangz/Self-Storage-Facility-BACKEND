using StorEase.Domain.Enums;

namespace StorEase.Domain.Entities;

public class UnitReturn
{
    public int UnitReturnId { get; set; }
    public int ContractId { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateOnly PlannedReturnDate { get; set; }
    public string? Reason { get; set; }
    public DateTime? InspectionSlot { get; set; }
    public RefundMethod? RefundMethod { get; set; }
    public string? RefundAccount { get; set; }
    public ReturnStatus Status { get; set; }

    public Contract Contract { get; set; } = null!;
}
