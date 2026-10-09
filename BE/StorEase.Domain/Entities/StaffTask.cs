using StorEase.Domain.Enums;

namespace StorEase.Domain.Entities;

public class StaffTask
{
    public int TaskId { get; set; }
    public int? ShiftId { get; set; }
    public int AssignedTo { get; set; }
    public TaskType TaskType { get; set; }
    public string? ReferenceCode { get; set; }
    public DateTime ScheduledAt { get; set; }
    public StaffTaskStatus Status { get; set; }

    public Shift? Shift { get; set; }
    public User AssignedToUser { get; set; } = null!;
}
