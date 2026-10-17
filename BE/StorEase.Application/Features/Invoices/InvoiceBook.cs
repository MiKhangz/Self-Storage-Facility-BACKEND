using StorEase.Application.Common;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Invoices;

/// <summary>Shared rules for creating and re-totalling invoices: deposit, monthly billing, late fees and adjustments.</summary>
public static class InvoiceBook
{
    public const string DepositDescription = "Tiền đặt cọc";
    public const string LateFeeDescription = "Phí trễ hạn";

    // Numbers come from what is billed, so they are unique by construction (one deposit per reservation,
    // one rent invoice per contract per month) and readable on a printed invoice.
    public static string DepositNumber(Reservation reservation) => $"DEP{reservation.ReservationCode}";

    public static string MonthlyNumber(int contractId, DateOnly period) => $"INV{period:yyyyMM}C{contractId}";

    public static InvoiceItem Line(string description, decimal unitAmount, int quantity = 1, int? feeTypeId = null) => new()
    {
        Description = description,
        Quantity = quantity,
        UnitAmount = Money.Round(unitAmount),
        LineAmount = Money.Round(unitAmount * quantity),
        FeeTypeId = feeTypeId,
    };

    /// <summary>
    /// Sets the total from the lines and moves the status to match the balance: fully paid is Paid; an unpaid
    /// balance past the due date is Overdue. <paramref name="invoice"/> must have its items loaded.
    /// </summary>
    public static void Retotal(Invoice invoice, DateOnly today)
    {
        invoice.TotalAmount = Money.Round(invoice.InvoiceItems.Sum(i => i.LineAmount));
        if (invoice.Status == InvoiceStatus.WrittenOff)
            return;
        invoice.Status = invoice.PaidAmount >= invoice.TotalAmount ? InvoiceStatus.Paid
            : invoice.Status == InvoiceStatus.Overdue || invoice.DueDate < today ? InvoiceStatus.Overdue
            : InvoiceStatus.Issued;
    }
}
