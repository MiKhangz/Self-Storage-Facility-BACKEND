using StorEase.Domain.Enums;

namespace StorEase.Domain.Entities;

public class Floor
{
    public int FloorId { get; set; }
    public int FacilityId { get; set; }
    public int FloorNumber { get; set; }
    public string? Name { get; set; }
    /// <summary>Only an Open floor's units can be booked.</summary>
    public FloorStatus Status { get; set; }

    public Facility Facility { get; set; } = null!;
}
