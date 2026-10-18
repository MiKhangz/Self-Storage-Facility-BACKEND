using StorEase.Domain.Enums;

namespace StorEase.Domain.Entities;

public class CheckIn
{
    public int CheckInId { get; set; }
    public int ReservationId { get; set; }
    public int? ContractId { get; set; }
    public int StorageUnitId { get; set; }
    public int StaffId { get; set; }
    public DateTime CheckInTime { get; set; }
    public string? LockSerial { get; set; }
    public string? CardNumber { get; set; }
    public string? ConditionNote { get; set; }
    public CheckInStatus Status { get; set; }

    public Reservation Reservation { get; set; } = null!;
    public Contract? Contract { get; set; }
    public StorageUnit StorageUnit { get; set; } = null!;
    public User Staff { get; set; } = null!;
}
