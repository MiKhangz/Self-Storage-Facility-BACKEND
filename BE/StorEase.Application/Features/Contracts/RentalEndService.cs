using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.DepositRefunds;
using StorEase.Application.Features.Invoices;
using StorEase.Application.Features.Pricing;
using StorEase.Application.Features.Returns;
using StorEase.Application.Features.StorageUnits;
using StorEase.Application.Features.Notifications;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Contracts;

/// <summary>
/// A rental near or past its end date, for the staff lists: Decision says whether the renter asked to renew or to hand
/// back; OverstayDays and OverstayFee are set once the end date has passed.
/// </summary>
public sealed record RentalEndDto(
    int ContractId, string ContractNumber, int CustomerId, string CustomerName, string CustomerPhone, string UnitCode,
    string FacilityName, DateOnly EndDate, int DaysLeft, string Decision, ContractStatus Status,
    DateTime? EndReminderAt, string? EndContactNote, int OverstayDays, decimal OverstayFee, bool CanMoveOut);

public sealed record RecordEndContactRequest(string Note);

/// <summary>What the daily run did: reminders sent, contracts that turned Overstay, overstay fees updated.</summary>
public sealed record RentalEndRunDto(int Reminded, int Overstayed, int FeesUpdated);

public interface IRentalEndService
{
    Task<RentalEndRunDto> RunAsync(DateOnly today, CancellationToken ct);
    /// <summary>Rentals ending within the reminder window, and those already past their end date, at the user's facilities.</summary>
    Task<Result<IReadOnlyList<RentalEndDto>>> ListAsync(bool overstayOnly, CancellationToken ct);
    Task<Result<RentalEndDto>> RecordContactAsync(int contractId, RecordEndContactRequest request, CancellationToken ct);
    /// <summary>
    /// After the overstay grace, or when unpaid rent reaches the clear-out stage, staff move the goods to the central
    /// warehouse: the contract closes, the deposit pays the overstay fee and any unpaid rent (the rest is refunded), and
    /// the unit goes to the next booking. The goods are kept there for the policy's keep days.
    /// </summary>
    Task<Result<RentalEndDto>> MoveToCentralStorageAsync(int contractId, CancellationToken ct);
}

public class RentalEndService(IUnitOfWork uow, ICurrentUser currentUser, IEmailService email) : IRentalEndService
{
    public const string OverstayFeeDescription = "Phí quá hạn trả kho";

    /// <summary>One invoice per overstaying contract, its single line growing each day.</summary>
    public const string OverstayPrefix = "OVS-";
    public static string OverstayNumber(int contractId) => $"{OverstayPrefix}C{contractId}";

    private static readonly ContractStatus[] Running = [ContractStatus.Active, ContractStatus.Overdue, ContractStatus.Suspended];

    public async Task<RentalEndRunDto> RunAsync(DateOnly today, CancellationToken ct)
    {
        var policy = await RentCalculator.PolicyForAsync(uow, today, ct);
        if (policy is null)
            return new RentalEndRunDto(0, 0, 0);
        var now = DateTime.UtcNow;
        var openReturns = uow.Repository<UnitReturn>().Query()
            .Where(r => r.Status != ReturnStatus.Completed && r.Status != ReturnStatus.Cancelled).Select(r => r.ContractId);
        var waitingRenewals = uow.Repository<Domain.Entities.Renewal>().Query()
            .Where(r => r.Status == Renewals.RenewalStatus.Requested || r.Status == Renewals.RenewalStatus.AwaitingCustomer).Select(r => r.ContractId);

        // An offer of another unit waits only until the end date: then it lapses and that unit is free again.
        var lapsed = await uow.Repository<Domain.Entities.Renewal>().Query()
            .Where(r => r.Status == Renewals.RenewalStatus.AwaitingCustomer && r.Contract.EndDate < today).ToListAsync(ct);
        foreach (var r in lapsed)
            r.Status = Renewals.RenewalStatus.Rejected;
        await uow.SaveChangesAsync(ct);

        // 1. "Your rental ends soon": once, when the end date comes within the reminder window and nothing is decided.
        var horizon = today.AddDays(policy.EndReminderDays);
        var toRemind = await uow.Repository<Contract>().Query().Include(c => c.Customer).Include(c => c.StorageUnit)
            .Where(c => Running.Contains(c.Status) && c.EndReminderAt == null && c.EndDate >= today && c.EndDate <= horizon
                && !openReturns.Contains(c.ContractId) && !waitingRenewals.Contains(c.ContractId))
            .ToListAsync(ct);
        foreach (var c in toRemind)
            c.EndReminderAt = now;

        // 2. Past the end date, not handed back and no renewal waiting for an answer: access stops and the overstay fee
        // starts. A renewal waiting for staff keeps the rental running; once it is rejected, the next run applies this.
        var overstaying = await uow.Repository<Contract>().Query().Include(c => c.AccessCodes)
            .Where(c => Running.Contains(c.Status) && c.EndDate < today && !openReturns.Contains(c.ContractId)
                && !waitingRenewals.Contains(c.ContractId))
            .ToListAsync(ct);
        foreach (var c in overstaying)
        {
            c.Status = ContractStatus.Overstay;
            foreach (var code in c.AccessCodes.Where(a => a.IsActive))
            {
                code.IsActive = false;
                code.SuspendedAt = now;
            }
        }
        await uow.SaveChangesAsync(ct);

        // 3. One growing fee line per overstaying contract: days past the end × a day's rent × the overstay percent.
        var fees = 0;
        var all = await uow.Repository<Contract>().Query().Where(c => c.Status == ContractStatus.Overstay).ToListAsync(ct);
        foreach (var c in all)
            if (await UpdateOverstayFeeAsync(c, policy, today, ct))
                fees++;
        await uow.SaveChangesAsync(ct);

        foreach (var c in toRemind)
            await NotifyAsync(c.Customer, $"/my-storage/units/{c.ContractId}", "Kỳ thuê kho sắp kết thúc",
                $"Kỳ thuê kho {c.StorageUnit.UnitCode} (hợp đồng {c.ContractNumber}) kết thúc ngày {c.EndDate:dd/MM/yyyy}. " +
                "Vui lòng gia hạn hoặc gửi yêu cầu trả kho trên StorEase; nhân viên sẽ gọi để xác nhận.", ct);
        return new RentalEndRunDto(toRemind.Count, overstaying.Count, fees);
    }

    public async Task<Result<IReadOnlyList<RentalEndDto>>> ListAsync(bool overstayOnly, CancellationToken ct)
    {
        var today = VietnamTime.Today;
        var policy = await RentCalculator.PolicyForAsync(uow, today, ct);
        var horizon = today.AddDays(policy?.EndReminderDays ?? 10);
        var query = Visible();
        query = overstayOnly
            ? query.Where(c => c.Status == ContractStatus.Overstay)
            : query.Where(c => c.Status == ContractStatus.Overstay || (Running.Contains(c.Status) && c.EndDate <= horizon));
        var contracts = await query.OrderBy(c => c.EndDate).Select(c => c.ContractId).ToListAsync(ct);
        var list = new List<RentalEndDto>();
        foreach (var id in contracts)
            list.Add((await ProjectAsync(id, policy, today, ct))!);
        return list;
    }

    public async Task<Result<RentalEndDto>> RecordContactAsync(int contractId, RecordEndContactRequest request, CancellationToken ct)
    {
        var contract = await Visible().FirstOrDefaultAsync(c => c.ContractId == contractId, ct);
        if (contract is null)
            return Error.NotFound("Contract not found.");
        var note = request.Note.Trim();
        contract.EndContactNote = $"{DateTime.UtcNow.AddHours(7):dd/MM HH:mm} · {note}";
        await uow.SaveChangesAsync(ct);
        var today = VietnamTime.Today;
        return (await ProjectAsync(contractId, await RentCalculator.PolicyForAsync(uow, today, ct), today, ct))!;
    }

    public async Task<Result<RentalEndDto>> MoveToCentralStorageAsync(int contractId, CancellationToken ct)
    {
        var today = VietnamTime.Today;
        var policy = await RentCalculator.PolicyForAsync(uow, today, ct);
        var moved = await uow.InTransactionAsync<int>(async () =>
        {
            var contract = await Visible().Include(c => c.AccessCodes).Include(c => c.StorageUnit)
                .FirstOrDefaultAsync(c => c.ContractId == contractId, ct);
            if (contract is null)
                return Error.NotFound("Contract not found.");
            // A lock-out leaves the return open, and settling it later would pay the deposit out a second time.
            if (await uow.Repository<UnitReturn>().Query().AnyAsync(r => r.ContractId == contract.ContractId
                    && r.Status != ReturnStatus.Completed && r.Status != ReturnStatus.Cancelled, ct))
                return Error.Conflict("This contract has an open return request; inspect and settle it instead of moving the goods.");
            var openCases = await uow.Repository<OverdueCase>().Query()
                .Where(o => o.ContractId == contract.ContractId && o.ResolvedAt == null).ToListAsync(ct);
            if (contract.Status == ContractStatus.Overstay)
            {
                var days = today.DayNumber - contract.EndDate.DayNumber;
                var wait = policy?.MoveOutAfterDays ?? 7;
                if (days < wait)
                    return Error.Conflict($"Goods can be moved out after {wait} days past the end date; it has been {days}.");
            }
            else if (!openCases.Any(o => o.Stage == OverdueStage.ClearOut))
                return Error.Conflict("Goods can be moved out only from an overstaying rental or an overdue case at the clear-out stage.");

            if (policy is not null)
                await UpdateOverstayFeeAsync(contract, policy, today, ct);
            await uow.SaveChangesAsync(ct);
            var outstanding = (await uow.Repository<Invoice>().Query()
                .Where(i => i.ContractId == contract.ContractId && (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.Overdue))
                .Select(i => i.TotalAmount - i.PaidAmount).ToListAsync(ct)).Sum();
            var (refund, _) = DepositSettlement.Calculate(contract.DepositAmount, 0m, outstanding);

            // Recorded as a hand-back: AwaitingPayout when a refund is owed in Deposit Refunds, or Completed immediately when zero.
            var unitReturn = new UnitReturn
            {
                ContractId = contract.ContractId, RequestedAt = DateTime.UtcNow, PlannedReturnDate = today, MoveOutDeadline = today,
                Reason = "Chuyển đồ về kho tổng sau khi quá hạn trả kho", Status = ReturnStatus.AwaitingPayout,
            };
            uow.Repository<UnitReturn>().Add(unitReturn);
            await uow.SaveChangesAsync(ct);
            var reservation = (await uow.Repository<Reservation>().FindAsync(contract.ReservationId, ct))!;
            var payout = DepositRefundService.Owe(uow, reservation, refund, unitReturn);
            if (payout?.Status == DepositRefundStatus.Paid)
                unitReturn.Status = ReturnStatus.Completed;
            await DepositCover.ApplyAsync(uow, contract, contract.DepositAmount, DateTime.UtcNow, ct);

            // What the deposit did not cover stays owed on its invoice; the case itself is finished.
            foreach (var o in openCases.Where(o => o.ResolvedAt == null))
            {
                o.ResolvedAt = DateTime.UtcNow;
                o.Stage = OverdueStage.Resolved;
            }
            contract.Status = ContractStatus.Closed;
            contract.MovedToCentralStorageAt = DateTime.UtcNow;
            foreach (var code in contract.AccessCodes.Where(a => a.IsActive))
                code.IsActive = false;
            await uow.SaveChangesAsync(ct);
            await UnitSchedule.ReleaseAsync(uow, contract.StorageUnit, ct);
            return contract.ContractId;
        }, ct);
        if (!moved.IsSuccess)
            return moved.Error;
        var customer = await uow.Repository<Contract>().Query().Where(c => c.ContractId == moved.Value)
            .Select(c => new { c.Customer, c.StorageUnit.UnitCode }).FirstAsync(ct);
        var keepUntil = today.AddDays(policy?.CentralStorageKeepDays ?? 90);
        await NotifyAsync(customer.Customer, "/payments", $"Đồ trong kho {customer.UnitCode} đã được chuyển về kho tổng",
            $"Đồ trong kho {customer.UnitCode} đã được chuyển về kho tổng của StorEase. Tiền cọc đã được dùng để trả các khoản còn nợ. " +
            $"Bạn có thể nhận lại đồ đến hết ngày {keepUntil:dd/MM/yyyy} sau khi thanh toán phần còn nợ (nếu có). " +
            "Sau ngày này, StorEase gửi thông báo cuối cùng trước khi xử lý số đồ không được nhận.", ct);
        return (await ProjectAsync(moved.Value, policy, today, ct))!;
    }

    private async Task<bool> UpdateOverstayFeeAsync(Contract contract, PricingPolicy policy, DateOnly today, CancellationToken ct)
    {
        var days = today.DayNumber - contract.EndDate.DayNumber;
        if (days < 1 || policy.OverstayFeePercent <= 0)
            return false;
        var fee = Money.Round(contract.MonthlyRent / 30m * policy.OverstayFeePercent / 100m * days);
        var number = OverstayNumber(contract.ContractId);
        var invoice = await uow.Repository<Invoice>().Query().Include(i => i.InvoiceItems)
            .FirstOrDefaultAsync(i => i.InvoiceNumber == number, ct);
        if (invoice is null)
        {
            invoice = new Invoice
            {
                ContractId = contract.ContractId, CustomerId = contract.CustomerId, InvoiceNumber = number,
                IssueDate = today, DueDate = today, Status = InvoiceStatus.Issued,
            };
            invoice.InvoiceItems.Add(InvoiceBook.Line(OverstayFeeDescription, fee));
            uow.Repository<Invoice>().Add(invoice);
        }
        else if (invoice.Status is InvoiceStatus.Paid or InvoiceStatus.WrittenOff)
            return false;
        else
        {
            var line = invoice.InvoiceItems.First();
            if (line.LineAmount == fee)
                return false;
            line.UnitAmount = line.LineAmount = fee;
        }
        invoice.DueDate = today;
        InvoiceBook.Retotal(invoice, today);
        return true;
    }

    private async Task<RentalEndDto?> ProjectAsync(int contractId, PricingPolicy? policy, DateOnly today, CancellationToken ct)
    {
        var c = await uow.Repository<Contract>().Query().AsNoTracking()
            .Include(x => x.Customer).Include(x => x.StorageUnit).ThenInclude(u => u.Facility)
            .FirstOrDefaultAsync(x => x.ContractId == contractId, ct);
        if (c is null)
            return null;
        var renewal = await uow.Repository<Domain.Entities.Renewal>().Query()
            .Where(r => r.ContractId == contractId && (r.Status == Renewals.RenewalStatus.Requested || r.Status == Renewals.RenewalStatus.AwaitingCustomer))
            .Select(r => r.Status).FirstOrDefaultAsync(ct);
        var returning = await uow.Repository<UnitReturn>().Query()
            .AnyAsync(r => r.ContractId == contractId && r.Status != ReturnStatus.Completed && r.Status != ReturnStatus.Cancelled, ct);
        var decision = returning ? "Return requested" : renewal is not null ? "Renewal requested" : "Undecided";
        var overstayDays = c.Status == ContractStatus.Overstay ? Math.Max(today.DayNumber - c.EndDate.DayNumber, 0) : 0;
        var fee = await uow.Repository<Invoice>().Query().Where(i => i.InvoiceNumber == OverstayNumber(contractId))
            .Select(i => i.TotalAmount).FirstOrDefaultAsync(ct);
        return new RentalEndDto(c.ContractId, c.ContractNumber, c.CustomerId, c.Customer.FullName, c.Customer.PhoneNumber ?? "",
            c.StorageUnit.UnitCode, c.StorageUnit.Facility.Name, c.EndDate, c.EndDate.DayNumber - today.DayNumber, decision, c.Status,
            c.EndReminderAt, c.EndContactNote, overstayDays, fee,
            c.Status == ContractStatus.Overstay && overstayDays >= (policy?.MoveOutAfterDays ?? 7));
    }

    /// <summary>Staff see the contracts of their facilities; the admin and operations see all.</summary>
    private IQueryable<Contract> Visible()
    {
        var query = uow.Repository<Contract>().Query();
        if (StaffScope.SeesAllFacilities(currentUser.Role))
            return query;
        var facilityIds = StaffScope.FacilityIdsOf(uow, currentUser.UserId);
        return query.Where(c => facilityIds.Contains(c.StorageUnit.FacilityId));
    }

    /// <summary>The bell always; email as well.</summary>
    private async Task NotifyAsync(User user, string link, string subject, string body, CancellationToken ct)
    {
        NotificationService.Add(uow, user.UserId, subject, body, link);
        await uow.SaveChangesAsync(ct);
        try { await email.SendAsync(user.Email, subject, body, ct); }
        catch { /* a reminder that fails to send is not worth failing the daily run */ }
    }
}
