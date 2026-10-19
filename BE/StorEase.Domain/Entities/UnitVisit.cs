namespace StorEase.Domain.Entities;

/// <summary>
/// A customer visiting the unit they rent, recorded by staff at reception (SR-05). Independent of AccessCode and
/// AccessLog. "Check-in" here means entering for a visit, not the start of the rental (<see cref="CheckIn"/>).
/// </summary>
public class UnitVisit
{
    public int UnitVisitId { get; set; }
    public int ContractId { get; set; }
    public int StorageUnitId { get; set; }
    public int RecordedBy { get; set; }
    public DateTime CheckInAt { get; set; }
    /// <summary>Null while the customer is still inside.</summary>
    public DateTime? CheckOutAt { get; set; }
    public string? Note { get; set; }

    public Contract Contract { get; set; } = null!;
    public StorageUnit StorageUnit { get; set; } = null!;
    public User RecordedByUser { get; set; } = null!;
}
