using StorEase.Domain.Enums;

namespace StorEase.Domain.Entities;

public class AccessLog
{
    public long AccessLogId { get; set; }
    public int AccessCodeId { get; set; }
    public int StorageUnitId { get; set; }
    public DateTime AccessTime { get; set; }
    public AccessResult Result { get; set; }
    public string? DeviceId { get; set; }

    public AccessCode AccessCode { get; set; } = null!;
    public StorageUnit StorageUnit { get; set; } = null!;
}
