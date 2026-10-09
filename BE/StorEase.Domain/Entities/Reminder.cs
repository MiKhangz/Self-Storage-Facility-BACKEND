using StorEase.Domain.Enums;

namespace StorEase.Domain.Entities;

public class Reminder
{
    public int ReminderId { get; set; }
    public int InvoiceId { get; set; }
    public ReminderChannel Channel { get; set; }
    public string Template { get; set; } = string.Empty;
    public DateTime? SentAt { get; set; }
    public ReminderStatus Status { get; set; }

    public Invoice Invoice { get; set; } = null!;
}
