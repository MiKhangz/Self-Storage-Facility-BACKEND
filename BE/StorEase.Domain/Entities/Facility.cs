using StorEase.Domain.Enums;

namespace StorEase.Domain.Entities;

public class Facility
{
    public int FacilityId { get; set; }
    public int? ManagerId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? OpeningHours { get; set; }
    public FacilityStatus Status { get; set; }

    public User? Manager { get; set; }
    public ICollection<Floor> Floors { get; set; } = [];
    public ICollection<StorageUnit> StorageUnits { get; set; } = [];
}
