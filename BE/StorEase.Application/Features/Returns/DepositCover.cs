using Microsoft.EntityFrameworkCore;
using StorEase.Application.Features.OverdueCases;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Returns;

/// <summary>When a rental closes, what is left of the deposit pays its open invoices, so none stays payable.</summary>
internal static class DepositCover
{
    /// <summary>
    /// Pays the contract's open invoices, the earliest due first, from <paramref name="available"/> (the deposit after
    /// damage and penalties). Each amount is recorded as a Deposit payment; what the deposit cannot cover stays open
    /// as the customer's shortfall. Also drops renewals still waiting, since the rental has ended. No save.
    /// </summary>
    public static async Task ApplyAsync(IUnitOfWork uow, Contract contract, decimal available, DateTime now, CancellationToken ct)
    {
        var open = await uow.Repository<Invoice>().Query()
            .Where(i => i.ContractId == contract.ContractId && (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.Overdue))
            .OrderBy(i => i.DueDate).ThenBy(i => i.InvoiceId)
            .ToListAsync(ct);
        foreach (var invoice in open)
        {
            var take = Math.Min(available, invoice.TotalAmount - invoice.PaidAmount);
            if (take <= 0)
                break;
            available -= take;
            uow.Repository<Payment>().Add(new Payment
            {
                InvoiceId = invoice.InvoiceId, PaymentCode = $"DPC{invoice.InvoiceId}", Method = PaymentMethod.Deposit,
                Amount = take, PaidAt = now, ReferenceNo = "Trừ vào tiền cọc", Status = PaymentStatus.Succeeded,
            });
            invoice.PaidAmount += take;
            if (invoice.PaidAmount >= invoice.TotalAmount)
                invoice.Status = InvoiceStatus.Paid;
            await OverdueRules.ResolvePaidAsync(uow, invoice, now, ct);
        }

        var waiting = await uow.Repository<Renewal>().Query()
            .Where(r => r.ContractId == contract.ContractId
                && (r.Status == Renewals.RenewalStatus.Requested || r.Status == Renewals.RenewalStatus.AwaitingCustomer))
            .ToListAsync(ct);
        foreach (var renewal in waiting)
            renewal.Status = Renewals.RenewalStatus.Rejected;
    }
}
