using StorEase.Application.Common;

namespace StorEase.Application.Features.Handovers;

/// <summary>Business rules 7 and 8, kept free of I/O so they are unit-tested directly.</summary>
public static class DepositSettlement
{
    /// <summary>
    /// Rule 8 (SR-04): handing the unit back before the contract's end date costs a percentage of the deposit;
    /// on or after the end date it costs nothing.
    /// </summary>
    public static decimal EarlyTerminationPenalty(decimal deposit, decimal percent, DateOnly returnDate, DateOnly endDate) =>
        returnDate < endDate ? Money.Round(deposit * percent / 100m) : 0m;

    /// <summary>Refund = deposit - damage - outstanding invoices, never below zero; any excess is the customer's shortfall.</summary>
    public static (decimal Refund, decimal Shortfall) Calculate(
        decimal deposit, decimal damageDeductions, decimal outstandingInvoices)
    {
        var balance = deposit - damageDeductions - outstandingInvoices;
        return (Math.Max(balance, 0m), Math.Max(-balance, 0m));
    }
}
