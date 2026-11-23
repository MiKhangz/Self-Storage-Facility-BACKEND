namespace StorEase.Domain.Entities;

public class StaffFacility
{
    public int StaffFacilityId { get; set; }
    public int UserId { get; set; }
    public int FacilityId { get; set; }
    public DateOnly AssignedDate { get; set; }
    /// <summary>The usual week at this facility, e.g. "Mon:Morning,Wed:Afternoon"; null means none.</summary>
    public string? Timetable { get; set; }
    /// <summary>Last day the timetable's shifts have been created for.</summary>
    public DateOnly? TimetableThrough { get; set; }

    public User User { get; set; } = null!;
    public Facility Facility { get; set; } = null!;
}
