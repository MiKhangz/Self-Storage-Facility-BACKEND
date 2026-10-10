using StorEase.Domain.Enums;

namespace StorEase.Domain.Entities;

public class Reservation
{
    public int ReservationId { get; set; }
    public int CustomerId { get; set; }
    public int FacilityId { get; set; }
    public int UnitTypeId { get; set; }
    public int? StorageUnitId { get; set; }
    public int? CreatedBy { get; set; }
    public string ReservationCode { get; set; } = string.Empty;
    public DateOnly MoveInDate { get; set; }
    public int RentalMonths { get; set; }
    public DateTime? CheckInSlot { get; set; }
    public ReservationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }

    public User Customer { get; set; } = null!;
    public Facility Facility { get; set; } = null!;
    public UnitType UnitType { get; set; } = null!;
    public StorageUnit? StorageUnit { get; set; }
    public User? CreatedByUser { get; set; }
}
