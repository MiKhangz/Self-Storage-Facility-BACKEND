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
    /// <summary>The policy version the booking was quoted under; its rent, deposit and rules stay with this rental.</summary>
    public int? PricingPolicyId { get; set; }
    /// <summary>The facility price version the rent was quoted from; null when the company standard price applied.</summary>
    public int? FacilityPriceVersionId { get; set; }
    /// <summary>The handover appointment the customer chose after paying the deposit; missing it forfeits the deposit.</summary>
    public DateTime? HandoverSlot { get; set; }
    /// <summary>Another unit staff (or the daily job) offer instead of the one held; the customer accepts or cancels with a full refund.</summary>
    public int? ProposedStorageUnitId { get; set; }
    public DateTime? ProposedAt { get; set; }
    /// <summary>
    /// StorEase has no usable unit for this booking. Set by the daily job; once the handover day comes it stays set, so a
    /// unit freed late that day does not turn our delay into the customer's no-show.
    /// </summary>
    public bool UnitBlocked { get; set; }
    /// <summary>Same-day booking: deposit at least 100,000 VND, discounted monthly rent upfront; customer cancellation/no-show at 20:30 retains 100,000 VND.</summary>
    public bool SameDay { get; set; }
    public UnitAssignmentMode UnitAssignmentMode { get; set; } = UnitAssignmentMode.AUTO;
    /// <summary>Comma-separated preference tags chosen for a STAFF_ASSISTED booking.</summary>
    public string? Preferences { get; set; }
    public string? PreferenceNote { get; set; }
    public ReservationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }

    public User Customer { get; set; } = null!;
    public Facility Facility { get; set; } = null!;
    public UnitType UnitType { get; set; } = null!;
    public StorageUnit? StorageUnit { get; set; }
    public StorageUnit? ProposedStorageUnit { get; set; }
    public User? CreatedByUser { get; set; }
    public PricingPolicy? PricingPolicy { get; set; }
    public FacilityPriceVersion? FacilityPriceVersion { get; set; }
}
