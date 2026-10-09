namespace StorEase.Domain.Entities;

public class TicketMessage
{
    public int MessageId { get; set; }
    public int TicketId { get; set; }
    public int SenderId { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool IsInternalNote { get; set; }
    public DateTime SentAt { get; set; }

    public SupportTicket Ticket { get; set; } = null!;
    public User Sender { get; set; } = null!;
}
