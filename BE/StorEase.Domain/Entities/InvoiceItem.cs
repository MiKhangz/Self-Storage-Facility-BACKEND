namespace StorEase.Domain.Entities;

public class InvoiceItem
{
    public int InvoiceItemId { get; set; }
    public int InvoiceId { get; set; }
    public int? FeeTypeId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitAmount { get; set; }
    public decimal LineAmount { get; set; }

    public Invoice Invoice { get; set; } = null!;
    public FeeType? FeeType { get; set; }
}
