using StorEase.Domain.Enums;

namespace StorEase.Domain.Entities;

public class HandoverInspection
{
    public int HandoverInspectionId { get; set; }
    public int HandoverId { get; set; }
    public int StorageUnitId { get; set; }
    public int StaffId { get; set; }
    public DateTime InspectedAt { get; set; }
    public ConditionResult ConditionResult { get; set; }
    public decimal CleaningFee { get; set; }
    public decimal RepairCharge { get; set; }
    public decimal OtherDeduction { get; set; }
    public decimal RefundAmount { get; set; }
    public HandoverInspectionStatus Status { get; set; }

    public Handover Handover { get; set; } = null!;
    public StorageUnit StorageUnit { get; set; } = null!;
    public User Staff { get; set; } = null!;
}
