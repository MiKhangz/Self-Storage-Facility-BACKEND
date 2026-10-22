using StorEase.Domain.Enums;

namespace StorEase.Domain.Entities;

/// <summary>
/// A deposit StorEase owes back: from a refundable cancellation before check-in, or from a handover settled with a
/// refund. Created Pending; Business Operations mark it Paid once the money has been handed over or transferred.
/// </summary>
public class DepositRefund
{
    public int DepositRefundId { get; set; }
    public int CustomerId { get; set; }
    public int FacilityId { get; set; }
    public int ReservationId { get; set; }
    /// <summary>Set when the refund comes from a handover settlement.</summary>
    public int? HandoverId { get; set; }
    public DepositRefundSource Source { get; set; }
    public decimal Amount { get; set; }
    /// <summary>The customer's choice on a handover; null after a cancellation until staff pay it.</summary>
    public RefundMethod? Method { get; set; }
    public string? RefundAccount { get; set; }
    public DepositRefundStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public int? PaidBy { get; set; }
    public string? ReferenceNo { get; set; }

    public User Customer { get; set; } = null!;
    public Facility Facility { get; set; } = null!;
    public Reservation Reservation { get; set; } = null!;
    public Handover? Handover { get; set; }
    public User? PaidByUser { get; set; }
}
