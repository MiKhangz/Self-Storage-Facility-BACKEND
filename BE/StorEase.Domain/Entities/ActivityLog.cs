using StorEase.Domain.Enums;

namespace StorEase.Domain.Entities;

public class ActivityLog
{
    public long LogId { get; set; }
    public int UserId { get; set; }
    public int? FacilityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? EntityName { get; set; }
    public string? EntityId { get; set; }
    public string? IpAddress { get; set; }
    public ActivityResult Result { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public Facility? Facility { get; set; }
}
