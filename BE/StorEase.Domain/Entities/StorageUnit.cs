using StorEase.Domain.Enums;

namespace StorEase.Domain.Entities;

public class StorageUnit
{
    public int StorageUnitId { get; set; }
    public int FacilityId { get; set; }
    public int FloorId { get; set; }
    public int UnitTypeId { get; set; }
    public string UnitCode { get; set; } = string.Empty;
    public string? Zone { get; set; }
    public StorageUnitStatus Status { get; set; }
    public decimal CurrentPrice { get; set; }
    public string? Note { get; set; }

    public Facility Facility { get; set; } = null!;
    public Floor Floor { get; set; } = null!;
    public UnitType UnitType { get; set; } = null!;
}
