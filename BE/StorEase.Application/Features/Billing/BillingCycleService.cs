using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Invoices;
using StorEase.Application.Features.OverdueCases;
using StorEase.Application.Features.Pricing;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Billing;

public sealed record BillingRunDto(
    DateOnly Date, int InvoicesIssued, int InvoicesMarkedOverdue, int CasesOpened, int LateFeesUpdated,
    int ContractsLockedOut, int CasesClearedOut, int RemindersSent, int RemindersFailed, int ReturnsLockedOut);

public interface IBillingCycleService
{
    /// <summary>
    /// The daily job (business rules 5 and 6): raises rent invoices, escalates overdue ones and sends reminders.
    /// Idempotent, so running it twice on the same day changes nothing the second time.
    /// </summary>
    Task<Result<BillingRunDto>> RunAsync(DateOnly today, CancellationToken ct);
}

public sealed class BillingCycleService(IUnitOfWork uow, IReminderService reminders) : IBillingCycleService
{
    /// <summary>Rent invoices appear this many days before they are due, so the customer can pay ahead.</summary>
    public const int IssueDaysAhead = 7;

    public const int DueSoonDays = 3;

    private static readonly ContractStatus[] BillableStatuses =
        [ContractStatus.Active, ContractStatus.Overdue, ContractStatus.Suspended, ContractStatus.PendingReturn];

    public async Task<Result<BillingRunDto>> RunAsync(DateOnly today, CancellationToken ct)
    {
        var policy = await RentCalculator.PolicyForAsync(uow, today, ct);
        var now = DateTime.UtcNow;

        var counts = await uow.InTransactionAsync<int[]>(async () =>
        {
            var issued = await IssueRentInvoicesAsync(today, ct);
            var escalation = policy is null ? new int[5] : await EscalateOverdueAsync(policy, today, now, ct);
            var returnsLocked = await LockPastMoveOutDeadlineAsync(today, now, ct);
            return new[] { issued }.Concat(escalation).Append(returnsLocked).ToArray();
        }, ct);
        if (!counts.IsSuccess)
            return counts.Error;

        var (sent, failed) = await SendScheduledRemindersAsync(today, ct);
        var c = counts.Value;
        return new BillingRunDto(today, c[0], c[1], c[2], c[3], c[4], c[5], sent, failed, c[6]);
    }

    /// <summary>
    /// One invoice per month of the contract, the k-th covering [StartDate + k months, StartDate + k+1 months).
    /// It falls due on the contract's payment day in that month, never before the period starts.
    /// </summary>
    public static DateOnly DueDateOf(Contract contract, DateOnly periodStart)
    {
        var day = Math.Min(contract.PaymentDay, DateTime.DaysInMonth(periodStart.Year, periodStart.Month));
        var due = new DateOnly(periodStart.Year, periodStart.Month, day);
        return due < periodStart ? periodStart : due;
    }

    private async Task<int> IssueRentInvoicesAsync(DateOnly today, CancellationToken ct)
    {
        var horizon = today.AddDays(IssueDaysAhead);
        var contracts = await uow.Repository<Contract>().Query()
            .Where(c => BillableStatuses.Contains(c.Status) && c.StartDate <= horizon)
            .Select(c => new { Contract = c, Numbers = c.Invoices.Select(i => i.InvoiceNumber).ToList() })
            .ToListAsync(ct);

        var issued = 0;
        foreach (var row in contracts)
        {
            var contract = row.Contract;
            // Months counted from the start date each time: stepping month by month would drift a 31st start to the 28th.
            for (var k = 0; ; k++)
            {
                var periodStart = contract.StartDate.AddMonths(k);
                if (periodStart > contract.EndDate || periodStart > horizon)
                    break;
                var due = DueDateOf(contract, periodStart);
                var number = InvoiceBook.MonthlyNumber(contract.ContractId, periodStart);
                if (due > horizon || row.Numbers.Contains(number))
                    continue;

                var invoice = new Invoice
                {
                    ContractId = contract.ContractId,
                    CustomerId = contract.CustomerId,
                    InvoiceNumber = number,
                    IssueDate = today,
                    DueDate = due,
                    Status = InvoiceStatus.Issued,
                };
                var periodEnd = contract.StartDate.AddMonths(k + 1).AddDays(-1);
                invoice.InvoiceItems.Add(InvoiceBook.Line(
                    $"Tiền thuê {periodStart:dd/MM/yyyy} – {(periodEnd < contract.EndDate ? periodEnd : contract.EndDate):dd/MM/yyyy}",
                    contract.MonthlyRent));
                InvoiceBook.Retotal(invoice, today);
                uow.Repository<Invoice>().Add(invoice);
                row.Numbers.Add(number);
                issued++;
            }
        }
        return issued;
    }

    /// <returns>marked overdue, cases opened, late fees updated, locked out, cleared out.</returns>
    private async Task<int[]> EscalateOverdueAsync(PricingPolicy policy, DateOnly today, DateTime now, CancellationToken ct)
    {
        int markedOverdue = 0, opened = 0, feesUpdated = 0, lockedOut = 0, clearedOut = 0;
        var invoices = await uow.Repository<Invoice>().Query().Include(i => i.InvoiceItems)
            .Where(i => (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.Overdue)
                && i.PaidAmount < i.TotalAmount && i.DueDate < today
                // A closed rental (settled from the deposit) or a cancelled handover is not chased any more.
                && (i.ContractId == null || (i.Contract!.Status != ContractStatus.Closed && i.Contract.Status != ContractStatus.Terminated)))
            .ToListAsync(ct);

        // Late fee, grace and lock-out days are the ones each contract was signed under, not today's policy.
        var termsOf = new Dictionary<int, PricingPolicy?>();
        async Task<PricingPolicy> TermsOfAsync(int contractId)
        {
            if (!termsOf.TryGetValue(contractId, out var pinned))
            {
                var c = await uow.Repository<Contract>().Query().Where(x => x.ContractId == contractId)
                    .Select(x => new { x.PricingPolicyId, x.StartDate }).FirstAsync(ct);
                termsOf[contractId] = pinned = await RentCalculator.PolicyOfAsync(uow, c.PricingPolicyId, c.StartDate, ct);
            }
            return pinned ?? policy;
        }

        foreach (var invoice in invoices)
        {
            if (invoice.Status != InvoiceStatus.Overdue)
            {
                invoice.Status = InvoiceStatus.Overdue;
                markedOverdue++;
            }
            var daysOverdue = today.DayNumber - invoice.DueDate.DayNumber;
            // A deposit has no contract to escalate; the unpaid deposit simply blocks the handover.
            if (invoice.ContractId is not { } contractId)
                continue;
            var terms = await TermsOfAsync(contractId);
            if (daysOverdue <= terms.GraceDays)
                continue;

            var overdue = await uow.Repository<OverdueCase>().Query()
                .FirstOrDefaultAsync(c => c.InvoiceId == invoice.InvoiceId && c.ResolvedAt == null, ct);
            if (overdue is null)
            {
                overdue = new OverdueCase
                {
                    ContractId = contractId,
                    InvoiceId = invoice.InvoiceId,
                    OverdueSince = invoice.DueDate.AddDays(1),
                    Stage = OverdueStage.Reminder,
                };
                uow.Repository<OverdueCase>().Add(overdue);
                var contract = await uow.Repository<Contract>().Query().FirstAsync(c => c.ContractId == contractId, ct);
                if (contract.Status == ContractStatus.Active)
                    contract.Status = ContractStatus.Overdue;
                opened++;
            }
            overdue.DaysOverdue = daysOverdue;

            if (UpdateLateFee(invoice, overdue, terms, daysOverdue, today))
                feesUpdated++;

            // AccessSuspendedAt on a LateFee case means a manager restored access by hand; respect that.
            if (overdue.Stage is OverdueStage.Reminder or OverdueStage.LateFee && overdue.AccessSuspendedAt is null
                && daysOverdue >= terms.SuspendAfterDays)
            {
                await OverdueRules.LockOutAsync(uow, overdue, now, ct);
                lockedOut++;
            }
            if (overdue.Stage == OverdueStage.Suspended && daysOverdue >= terms.ClearOutAfterDays)
            {
                overdue.Stage = OverdueStage.ClearOut;
                clearedOut++;
            }
        }
        return [markedOverdue, opened, feesUpdated, lockedOut, clearedOut];
    }

    /// <summary>
    /// A return still not inspected after its move-out deadline: the customer's codes stop working and staff clear
    /// the unit out, then inspect and settle as usual.
    /// </summary>
    private async Task<int> LockPastMoveOutDeadlineAsync(DateOnly today, DateTime now, CancellationToken ct)
    {
        var late = await uow.Repository<UnitReturn>().Query()
            .Where(r => (r.Status == ReturnStatus.Requested || r.Status == ReturnStatus.Approved)
                && r.MoveOutDeadline < today && r.AccessLockedAt == null)
            .ToListAsync(ct);
        foreach (var unitReturn in late)
        {
            var codes = await uow.Repository<AccessCode>().Query()
                .Where(a => a.ContractId == unitReturn.ContractId && a.IsActive).ToListAsync(ct);
            foreach (var code in codes)
                code.IsActive = false;
            unitReturn.AccessLockedAt = now;
        }
        return late.Count;
    }

    /// <summary>
    /// The late fee is a percentage of the billed rent for every full week past the grace period, kept as one line
    /// on the invoice that grows each week.
    /// </summary>
    private static bool UpdateLateFee(Invoice invoice, OverdueCase overdue, PricingPolicy policy, int daysOverdue, DateOnly today)
    {
        var weeks = (daysOverdue - policy.GraceDays) / 7;
        if (weeks < 1 || policy.LateFeePercentPerWeek <= 0)
            return false;
        var feeLine = invoice.InvoiceItems.FirstOrDefault(i => i.Description == InvoiceBook.LateFeeDescription);
        var principal = invoice.InvoiceItems.Where(i => i != feeLine).Sum(i => i.LineAmount);
        var fee = Money.Round(principal * policy.LateFeePercentPerWeek / 100m * weeks);
        if (feeLine?.LineAmount == fee)
            return false;

        if (feeLine is null)
            invoice.InvoiceItems.Add(InvoiceBook.Line(InvoiceBook.LateFeeDescription, fee));
        else
            feeLine.UnitAmount = feeLine.LineAmount = fee;
        InvoiceBook.Retotal(invoice, today);
        overdue.LateFeeAmount = fee;
        if (overdue.Stage == OverdueStage.Reminder)
            overdue.Stage = OverdueStage.LateFee;
        return true;
    }

    private async Task<(int Sent, int Failed)> SendScheduledRemindersAsync(DateOnly today, CancellationToken ct)
    {
        var dueSoon = today.AddDays(DueSoonDays);
        var candidates = await uow.Repository<Invoice>().Query()
            .Where(i => i.PaidAmount < i.TotalAmount
                && ((i.Status == InvoiceStatus.Issued && i.DueDate == dueSoon) || i.Status == InvoiceStatus.Overdue))
            .Select(i => new { i.InvoiceId, i.Status, i.DueDate })
            .ToListAsync(ct);
        // Overdue: the day after it falls due, then once a week.
        var targets = candidates.Where(i => i.Status == InvoiceStatus.Issued || (today.DayNumber - i.DueDate.DayNumber) % 7 == 1);

        int sent = 0, failed = 0;
        foreach (var target in targets)
        {
            var template = target.Status == InvoiceStatus.Issued ? ReminderTemplates.DueSoon : ReminderTemplates.Overdue;
            var outcome = await reminders.SendAsync(target.InvoiceId, template, onlyIfOptedIn: true, ct);
            if (outcome == ReminderStatus.Sent) sent++;
            else if (outcome == ReminderStatus.Failed) failed++;
        }
        return (sent, failed);
    }
}
