namespace StorEase.Domain.Entities;

public class StaffFacility
{
    public int StaffFacilityId { get; set; }
    public int UserId { get; set; }
    public int FacilityId { get; set; }
    public DateOnly AssignedDate { get; set; }

    public User User { get; set; } = null!;
    public Facility Facility { get; set; } = null!;
}
