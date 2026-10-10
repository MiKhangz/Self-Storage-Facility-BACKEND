using StorEase.Domain.Enums;

namespace StorEase.Domain.Entities;

public class SupportTicket
{
    public int TicketId { get; set; }
    public int CustomerId { get; set; }
    public int? StorageUnitId { get; set; }
    public int FacilityId { get; set; }
    public int CategoryId { get; set; }
    public int? AssignedTo { get; set; }
    public string TicketCode { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public Priority Priority { get; set; }
    public SupportTicketStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public User Customer { get; set; } = null!;
    public StorageUnit? StorageUnit { get; set; }
    public Facility Facility { get; set; } = null!;
    public TicketCategory Category { get; set; } = null!;
    public User? AssignedToUser { get; set; }
    public ICollection<TicketMessage> TicketMessages { get; set; } = [];
}
