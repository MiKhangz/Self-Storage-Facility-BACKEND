using StorEase.Domain.Enums;

namespace StorEase.Domain.Entities;

public class Shift
{
    public int ShiftId { get; set; }
    public int UserId { get; set; }
    public int FacilityId { get; set; }
    public DateOnly ShiftDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public ShiftType ShiftType { get; set; }

    public User User { get; set; } = null!;
    public Facility Facility { get; set; } = null!;
}
