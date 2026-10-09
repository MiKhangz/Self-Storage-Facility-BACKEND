using StorEase.Domain.Enums;

namespace StorEase.Domain.Entities;

public class Payment
{
    public int PaymentId { get; set; }
    public int InvoiceId { get; set; }
    public int? ReceivedBy { get; set; }
    public string PaymentCode { get; set; } = string.Empty;
    public PaymentMethod Method { get; set; }
    public decimal Amount { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? ReferenceNo { get; set; }
    public PaymentStatus Status { get; set; }

    public Invoice Invoice { get; set; } = null!;
    public User? ReceivedByUser { get; set; }
}
