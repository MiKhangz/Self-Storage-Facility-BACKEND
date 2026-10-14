using Microsoft.EntityFrameworkCore;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.OverdueCases;

/// <summary>
/// Business rule 6: an overdue case escalates reminder → late fee → lockout. Lockout suspends the contract's access
/// codes (marked with <c>SuspendedAt</c>) but never terminates the contract; paying the invoice in full lifts it.
/// </summary>
public static class OverdueRules
{
    /// <summary>Suspends the contract's active codes. The caller owns the transaction and the save.</summary>
    public static async Task LockOutAsync(IUnitOfWork uow, OverdueCase overdue, DateTime now, CancellationToken ct)
    {
        var contract = await uow.Repository<Contract>().Query().Include(c => c.AccessCodes)
            .FirstAsync(c => c.ContractId == overdue.ContractId, ct);
        foreach (var code in contract.AccessCodes.Where(a => a.IsActive))
        {
            code.IsActive = false;
            code.SuspendedAt = now;
        }
        contract.Status = ContractStatus.Suspended;
        overdue.Stage = OverdueStage.Suspended;
        overdue.AccessSuspendedAt = now;
    }

    /// <summary>
    /// Closes the open cases of a fully paid invoice and, when the contract has no other open case, makes it Active
    /// again and restores the codes the lockout suspended (not ones revoked for other reasons). No save.
    /// </summary>
    public static async Task ResolvePaidAsync(IUnitOfWork uow, Invoice invoice, DateTime now, CancellationToken ct)
    {
        if (invoice.Status != InvoiceStatus.Paid)
            return;
        var cases = await uow.Repository<OverdueCase>().Query()
            .Where(c => c.InvoiceId == invoice.InvoiceId && c.ResolvedAt == null).ToListAsync(ct);
        foreach (var overdue in cases)
        {
            overdue.ResolvedAt = now;
            overdue.Stage = OverdueStage.Resolved;
            var stillOpen = await uow.Repository<OverdueCase>().Query().AnyAsync(c => c.ContractId == overdue.ContractId
                && c.ResolvedAt == null && c.OverdueCaseId != overdue.OverdueCaseId, ct);
            if (!stillOpen)
                await LiftAsync(uow, overdue.ContractId, now, ct);
        }
    }

    public static async Task LiftAsync(IUnitOfWork uow, int contractId, DateTime now, CancellationToken ct)
    {
        var contract = await uow.Repository<Contract>().Query().Include(c => c.AccessCodes)
            .FirstAsync(c => c.ContractId == contractId, ct);
        if (contract.Status is ContractStatus.Overdue or ContractStatus.Suspended)
            contract.Status = ContractStatus.Active;
        foreach (var code in contract.AccessCodes.Where(a => a.SuspendedAt != null && !a.IsActive && a.ExpiresAt > now))
        {
            code.IsActive = true;
            code.SuspendedAt = null;
        }
    }
}
