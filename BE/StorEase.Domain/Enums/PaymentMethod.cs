namespace StorEase.Domain.Enums;

public enum PaymentMethod
{
    Cash,
    BankTransfer,
    Gateway,
    Card,
    /// <summary>Taken from the deposit when the rental closed.</summary>
    Deposit
}
