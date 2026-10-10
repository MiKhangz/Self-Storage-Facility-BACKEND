using StorEase.Domain.Enums;

namespace StorEase.Domain.Entities;

public class ReturnInspection
{
    public int ReturnInspectionId { get; set; }
    public int MoveOutRequestId { get; set; }
    public int StorageUnitId { get; set; }
    public int StaffId { get; set; }
    public DateTime InspectedAt { get; set; }
    public ConditionResult ConditionResult { get; set; }
    public decimal CleaningFee { get; set; }
    public decimal RepairCharge { get; set; }
    public decimal OtherDeduction { get; set; }
    public decimal RefundAmount { get; set; }
    public ReturnInspectionStatus Status { get; set; }

    public MoveOutRequest MoveOutRequest { get; set; } = null!;
    public StorageUnit StorageUnit { get; set; } = null!;
    public User Staff { get; set; } = null!;
}
