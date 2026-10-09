using StorEase.Domain.Enums;

namespace StorEase.Domain.Entities;

public class Invoice
{
    public int InvoiceId { get; set; }
    public int? ContractId { get; set; }
    public int? ReservationId { get; set; }
    public int CustomerId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public InvoiceStatus Status { get; set; }

    public Contract? Contract { get; set; }
    public Reservation? Reservation { get; set; }
    public User Customer { get; set; } = null!;
    public ICollection<InvoiceItem> InvoiceItems { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
}
