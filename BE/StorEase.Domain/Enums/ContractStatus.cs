namespace StorEase.Domain.Enums;

public enum ContractStatus
{
    Draft,
    Active,
    PendingReturn,
    Closed,
    Overdue,
    Suspended,
    Terminated,
    /// <summary>The end date has passed and the unit was not handed back: a daily fee runs and access is locked.</summary>
    Overstay
}
