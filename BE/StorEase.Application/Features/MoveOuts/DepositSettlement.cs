namespace StorEase.Application.Features.MoveOuts;

/// <summary>Business rules 7 and 8, kept free of I/O so they are unit-tested directly.</summary>
public static class DepositSettlement
{
    /// <summary>Earliest move-out date a request may name: today plus the notice period.</summary>
    public static DateOnly EarliestMoveOutDate(DateOnly today, int noticeDays) => today.AddDays(noticeDays);

    /// <summary>Refund = deposit - damage - outstanding invoices, never below zero; any excess is the customer's shortfall.</summary>
    public static (decimal Refund, decimal Shortfall) Calculate(
        decimal deposit, decimal damageDeductions, decimal outstandingInvoices)
    {
        var balance = deposit - damageDeductions - outstandingInvoices;
        return (Math.Max(balance, 0m), Math.Max(-balance, 0m));
    }
}
