using StorEase.Domain.Enums;

namespace StorEase.Domain.Entities;

public class MaintenanceOrder
{
    public int MaintenanceOrderId { get; set; }
    public int StorageUnitId { get; set; }
    public int? TicketId { get; set; }
    public int? AssignedTo { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime? ScheduledAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public decimal? Cost { get; set; }
    public MaintenanceOrderStatus Status { get; set; }

    public StorageUnit StorageUnit { get; set; } = null!;
    public SupportTicket? Ticket { get; set; }
    public User? AssignedToUser { get; set; }
}
