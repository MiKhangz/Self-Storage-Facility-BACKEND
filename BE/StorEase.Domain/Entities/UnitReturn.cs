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

    /// <summary>The customer has until this day (request date + 15) to empty the unit; after it their access is locked.</summary>
    public DateOnly MoveOutDeadline { get; set; }
    /// <summary>Set by the daily job when the deadline passed with the unit still not inspected; staff then clear it out.</summary>
    public DateTime? AccessLockedAt { get; set; }

    public Contract Contract { get; set; } = null!;
}
