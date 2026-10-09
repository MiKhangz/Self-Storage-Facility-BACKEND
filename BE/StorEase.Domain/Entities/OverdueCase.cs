using StorEase.Domain.Enums;

namespace StorEase.Domain.Entities;

public class OverdueCase
{
    public int OverdueCaseId { get; set; }
    public int ContractId { get; set; }
    public int? InvoiceId { get; set; }
    public DateOnly OverdueSince { get; set; }
    public int DaysOverdue { get; set; }
    public decimal LateFeeAmount { get; set; }
    public OverdueStage Stage { get; set; }
    public DateTime? AccessSuspendedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public Contract Contract { get; set; } = null!;
    public Invoice? Invoice { get; set; }
}
