namespace StorEase.Domain.Enums;

public enum DepositRefundSource
{
    Cancellation,
    Return,
    /// <summary>Money that arrived after the booking had closed.</summary>
    LatePayment
}
