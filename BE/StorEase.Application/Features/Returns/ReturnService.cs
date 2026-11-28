using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.StorageUnits;
using StorEase.Application.Features.DepositRefunds;
using StorEase.Application.Features.Invoices;
using StorEase.Application.Features.Notifications;
using StorEase.Application.Features.Pricing;
using StorEase.Application.Features.Returns.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Returns;

public interface IReturnService
{
    Task<Result<ReturnDto>> RequestAsync(CreateReturnRequest request, CancellationToken ct);
    Task<Result<ReturnDto>> GetAsync(int id, CancellationToken ct);
    Task<Result<PagedResult<ReturnDto>>> ListAsync(ReturnStatus? status, int page, int pageSize, CancellationToken ct);
    Task<Result<ReturnDto>> ApproveAsync(int id, ApproveReturnRequest request, CancellationToken ct);
    Task<Result<ReturnDto>> InspectAsync(int id, CreateReturnInspectionRequest request, CancellationToken ct);
    Task<Result<DepositSettlementDto>> SettleAsync(int id, CancellationToken ct);
}

public class ReturnService(IUnitOfWork uow, ICurrentUser currentUser) : IReturnService
{
    /// <summary>Days the customer has to empty the unit, counted from the return request.</summary>
    public const int MoveOutDays = 15;

    public Task<Result<ReturnDto>> RequestAsync(CreateReturnRequest request, CancellationToken ct) =>
        RequestAsync(request, atEndDate: false, ct);

    /// <summary>
    /// <paramref name="atEndDate"/>: the renter hands back on the contract's end date (they declined to renew), so the
    /// date may be beyond the usual move-out window and is also the move-out deadline.
    /// </summary>
    internal async Task<Result<ReturnDto>> RequestAsync(CreateReturnRequest request, bool atEndDate, CancellationToken ct)
    {
        var created = await uow.InTransactionAsync<int>(async () =>
        {
            var contract = await uow.Repository<Contract>().Query().Include(c => c.AccessCodes)
                .FirstOrDefaultAsync(c => c.ContractId == request.ContractId, ct);
            // Someone else's contract is a 404, not a 403, so ids cannot be probed.
            if (contract is null || contract.CustomerId != currentUser.UserId)
                return Error.NotFound("Contract not found.");
            var overstaying = contract.Status == ContractStatus.Overstay;
            if (contract.Status != ContractStatus.Active && !overstaying)
                return Error.Conflict($"Only an Active or overstaying contract can be handed back; this one is {contract.Status}.");

            var hasUnresolvedPayment = await uow.Repository<Invoice>().Query().AnyAsync(i =>
                i.ContractId == contract.ContractId &&
                (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.Overdue) &&
                i.PaidAmount < i.TotalAmount &&
                !i.InvoiceNumber.StartsWith("OVS-"), ct);
            if (hasUnresolvedPayment)
                return Error.Conflict("Please pay your outstanding invoices before requesting a return.");

            var open = await uow.Repository<UnitReturn>().Query().AnyAsync(m =>
                m.ContractId == contract.ContractId &&
                (m.Status == ReturnStatus.Requested || m.Status == ReturnStatus.Approved ||
                 m.Status == ReturnStatus.Inspected || m.Status == ReturnStatus.AwaitingPayout), ct);
            if (open)
                return Error.Conflict("This contract already has an open return request.");

            // Rule 8 (SR-04): no notice period; an early return is allowed and costs a penalty at settlement.
            var today = VietnamTime.Today;
            if (request.PlannedReturnDate < today)
                return Error.Validation("The return date cannot be in the past.");
            var deadline = atEndDate ? request.PlannedReturnDate : today.AddDays(MoveOutDays);
            if (request.PlannedReturnDate > deadline)
                return Error.Validation($"The unit must be emptied within {MoveOutDays} days, by {deadline:dd/MM/yyyy}.");

            var unitReturn = new UnitReturn
            {
                ContractId = contract.ContractId,
                RequestedAt = DateTime.UtcNow,
                PlannedReturnDate = request.PlannedReturnDate,
                Reason = request.Reason,
                RefundMethod = request.RefundMethod,
                RefundAccount = request.RefundAccount,
                Status = ReturnStatus.Requested,
                MoveOutDeadline = deadline,
            };
            uow.Repository<UnitReturn>().Add(unitReturn);
            contract.Status = ContractStatus.PendingReturn;
            // Past the end date the codes were suspended; the renter gets them back to empty the unit. The fee stops here.
            if (overstaying)
                foreach (var code in contract.AccessCodes.Where(a => !a.IsActive && a.SuspendedAt != null))
                {
                    code.IsActive = true;
                    code.SuspendedAt = null;
                    code.ExpiresAt = deadline.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                }
            await uow.SaveChangesAsync(ct);
            return unitReturn.UnitReturnId;
        }, ct);

        return created.IsSuccess ? await GetAsync(created.Value, ct) : created.Error;
    }

    public async Task<Result<ReturnDto>> GetAsync(int id, CancellationToken ct)
    {
        var dto = await Project(Visible().Where(m => m.UnitReturnId == id)).FirstOrDefaultAsync(ct);
        return dto is null ? Error.NotFound("Return request not found.") : dto;
    }

    /// <summary>The staff queue is <c>status=Approved</c>: nearest planned date first.</summary>
    public async Task<Result<PagedResult<ReturnDto>>> ListAsync(
        ReturnStatus? status, int page, int pageSize, CancellationToken ct)
    {
        // Reconcile any return settled under the previous logic (marked Completed while its DepositRefund is still Pending).
        var pendingPayoutReturns = await uow.Repository<UnitReturn>().Query()
            .Where(m => m.Status == ReturnStatus.Completed
                && uow.Repository<DepositRefund>().Query().Any(d => d.UnitReturnId == m.UnitReturnId && d.Status == DepositRefundStatus.Pending))
            .ToListAsync(ct);
        if (pendingPayoutReturns.Count > 0)
        {
            foreach (var r in pendingPayoutReturns)
                r.Status = ReturnStatus.AwaitingPayout;
            await uow.SaveChangesAsync(ct);
        }

        var query = Visible();
        if (status is not null)
            query = query.Where(m => m.Status == status);

        return await Project(query.OrderBy(m => m.PlannedReturnDate).ThenBy(m => m.UnitReturnId))
            .ToPagedResultAsync(page, pageSize, ct);
    }

    public async Task<Result<ReturnDto>> ApproveAsync(int id, ApproveReturnRequest request, CancellationToken ct)
    {
        var unitReturn = await Visible().FirstOrDefaultAsync(m => m.UnitReturnId == id, ct);
        if (unitReturn is null)
            return Error.NotFound("Return request not found.");
        if (unitReturn.Status != ReturnStatus.Requested)
            return Error.Conflict($"Only a Requested return can be approved; this one is {unitReturn.Status}.");

        unitReturn.InspectionSlot = request.InspectionSlot;
        unitReturn.Status = ReturnStatus.Approved;
        await uow.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<Result<ReturnDto>> InspectAsync(
        int id, CreateReturnInspectionRequest request, CancellationToken ct)
    {
        var inspected = await uow.InTransactionAsync<int>(async () =>
        {
            var unitReturn = await Visible().FirstOrDefaultAsync(m => m.UnitReturnId == id, ct);
            if (unitReturn is null)
                return Error.NotFound("Return request not found.");
            if (unitReturn.Status != ReturnStatus.Approved)
                return Error.Conflict($"Only an Approved return can be inspected; this one is {unitReturn.Status}.");

            var contract = (await uow.Repository<Contract>().FindAsync(unitReturn.ContractId, ct))!;
            uow.Repository<ReturnInspection>().Add(new ReturnInspection
            {
                UnitReturnId = unitReturn.UnitReturnId,
                StorageUnitId = contract.StorageUnitId,
                StaffId = currentUser.UserId,
                InspectedAt = DateTime.UtcNow,
                ConditionResult = request.ConditionResult,
                CleaningFee = request.CleaningFee,
                RepairCharge = request.RepairCharge,
                OtherDeduction = request.OtherDeduction,
                RefundAmount = 0m, // set by settlement; the deposit is not refundable before then
                Status = ReturnInspectionStatus.Completed,
            });
            unitReturn.Status = ReturnStatus.Inspected;
            return unitReturn.UnitReturnId;
        }, ct);

        return inspected.IsSuccess ? await GetAsync(inspected.Value, ct) : inspected.Error;
    }

    /// <summary>
    /// Business rule 7. Records the refund and closes the contract. The refund is owed as a Pending DepositRefund,
    /// which Business Operations mark Paid once the customer has the money.
    /// </summary>
    public Task<Result<DepositSettlementDto>> SettleAsync(int id, CancellationToken ct) =>
        uow.InTransactionAsync<DepositSettlementDto>(async () =>
        {
            var unitReturn = await Visible().FirstOrDefaultAsync(m => m.UnitReturnId == id, ct);
            if (unitReturn is null)
                return Error.NotFound("Return request not found.");
            if (unitReturn.Status != ReturnStatus.Inspected)
                return Error.Conflict($"A deposit is settled only after the return inspection; this return is {unitReturn.Status}.");

            var inspection = await uow.Repository<ReturnInspection>().Query()
                .Where(i => i.UnitReturnId == id)
                .OrderByDescending(i => i.ReturnInspectionId)
                .FirstAsync(ct);
            if (inspection.Status != ReturnInspectionStatus.Completed)
                return Error.Conflict($"The return inspection is {inspection.Status}; resolve it before settling.");

            var contract = (await uow.Repository<Contract>().FindAsync(unitReturn.ContractId, ct))!;
            var outstanding = (await uow.Repository<Invoice>().Query()
                .Where(i => i.ContractId == contract.ContractId &&
                            (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.Overdue))
                .Select(i => i.TotalAmount - i.PaidAmount)
                .ToListAsync(ct)).Sum(); // a contract has a handful of invoices; summing here keeps SQLite (tests) working

            var damage = inspection.CleaningFee + inspection.RepairCharge + inspection.OtherDeduction;
            // The rate is the one the customer agreed to: the contract's own policy version, like the rent (rule 4).
            var policy = await RentCalculator.PolicyOfAsync(uow, contract.PricingPolicyId, contract.StartDate, ct);
            var penalty = DepositSettlement.EarlyTerminationPenalty(
                contract.DepositAmount, policy?.EarlyTerminationPercent ?? 0m, unitReturn.PlannedReturnDate, contract.EndDate);
            var otherInspectionFee = inspection.OtherDeduction;
            inspection.OtherDeduction += penalty;
            var (refund, shortfall) = DepositSettlement.Calculate(contract.DepositAmount, damage + penalty, outstanding);

            inspection.RefundAmount = refund;
            await DepositCover.ApplyAsync(uow, contract, Math.Max(contract.DepositAmount - damage - penalty, 0m), DateTime.UtcNow, ct);
            var reservation = (await uow.Repository<Reservation>().FindAsync(contract.ReservationId, ct))!;
            var payout = DepositRefundService.Owe(uow, reservation, refund, unitReturn);

            var unit = (await uow.Repository<StorageUnit>().FindAsync(contract.StorageUnitId, ct))!;
            var damageShortfall = Money.Round(Math.Max(damage + penalty - contract.DepositAmount, 0m));
            int? shortfallInvoiceId = null;
            string? shortfallInvoiceNumber = null;
            if (damageShortfall > 0m)
            {
                var today = VietnamTime.Today;
                var shortfallNumber = InvoiceBook.DamageShortfallNumber(unitReturn.UnitReturnId);
                var shortfallInvoice = new Invoice
                {
                    ContractId = contract.ContractId,
                    CustomerId = contract.CustomerId,
                    InvoiceNumber = shortfallNumber,
                    IssueDate = today,
                    DueDate = today.AddDays(7),
                    Status = InvoiceStatus.Issued,
                };
                if (inspection.CleaningFee > 0m)
                    shortfallInvoice.InvoiceItems.Add(InvoiceBook.Line($"Phí vệ sinh trả kho {unit.UnitCode}", inspection.CleaningFee));
                if (inspection.RepairCharge > 0m)
                    shortfallInvoice.InvoiceItems.Add(InvoiceBook.Line($"Phí sửa chữa trả kho {unit.UnitCode}", inspection.RepairCharge));
                if (otherInspectionFee > 0m)
                    shortfallInvoice.InvoiceItems.Add(InvoiceBook.Line($"Khấu trừ khác trả kho {unit.UnitCode}", otherInspectionFee));
                if (penalty > 0m)
                    shortfallInvoice.InvoiceItems.Add(InvoiceBook.Line($"Phí trả kho trước hạn {unit.UnitCode}", penalty));
                if (contract.DepositAmount > 0m)
                    shortfallInvoice.InvoiceItems.Add(InvoiceBook.Line("Trừ vào tiền cọc đang giữ", -contract.DepositAmount));
                InvoiceBook.Retotal(shortfallInvoice, today);
                uow.Repository<Invoice>().Add(shortfallInvoice);
                await uow.SaveChangesAsync(ct);
                shortfallInvoiceId = shortfallInvoice.InvoiceId;
                shortfallInvoiceNumber = shortfallInvoice.InvoiceNumber;

                NotificationService.Add(
                    uow,
                    contract.CustomerId,
                    $"Phát sinh hóa đơn bồi thường kho {unit.UnitCode}",
                    $"Chi phí kiểm tra trả kho {unit.UnitCode} vượt tiền cọc đang giữ. Hóa đơn {shortfallNumber} ({Money.Text(damageShortfall)} ₫) đã được tạo, hạn thanh toán {shortfallInvoice.DueDate:dd/MM/yyyy}.",
                    "/payments");
            }

            // Settle moves the return to AwaitingPayout; if the refund is zero, the payout record is Paid immediately
            // and the return closes to Completed in the same transaction.
            unitReturn.Status = ReturnStatus.AwaitingPayout;
            if (payout?.Status == DepositRefundStatus.Paid)
                unitReturn.Status = ReturnStatus.Completed;
            contract.Status = ContractStatus.Closed;

            var codes = await uow.Repository<AccessCode>().Query()
                .Where(a => a.ContractId == contract.ContractId).ToListAsync(ct);
            foreach (var code in codes)
                code.IsActive = false;

            // ponytail: a unit with major damage goes to Maintenance; staff flip it back through the unit board.
            if (inspection.ConditionResult == ConditionResult.MajorDamage)
                unit.Status = StorageUnitStatus.Maintenance;
            else
                await UnitSchedule.ReleaseAsync(uow, unit, ct); // held for the next booking, else back on sale

            return new DepositSettlementDto(
                unitReturn.UnitReturnId, contract.DepositAmount, damage, penalty, outstanding, refund, shortfall, unitReturn.Status,
                shortfallInvoiceId, shortfallInvoiceNumber);
        }, ct);

    /// <summary>Customers see their own; facility staff and managers the facilities they work at; admin and business ops all.</summary>
    private IQueryable<UnitReturn> Visible()
    {
        var query = uow.Repository<UnitReturn>().Query();
        var userId = currentUser.UserId;
        if (currentUser.IsCustomer)
            return query.Where(m => m.Contract.CustomerId == userId);
        if (StaffScope.SeesAllFacilities(currentUser.Role))
            return query;
        var facilityIds = StaffScope.FacilityIdsOf(uow, userId);
        return query.Where(m => facilityIds.Contains(m.Contract.StorageUnit.FacilityId));
    }

    private IQueryable<ReturnDto> Project(IQueryable<UnitReturn> source)
    {
        var inspections = uow.Repository<ReturnInspection>().Query();
        var refunds = uow.Repository<DepositRefund>().Query();
        return source.Select(m => new ReturnDto(
            m.UnitReturnId,
            m.ContractId,
            m.Contract.ContractNumber,
            m.Contract.CustomerId,
            m.Contract.Customer.FullName,
            m.Contract.StorageUnitId,
            m.Contract.StorageUnit.UnitCode,
            m.RequestedAt,
            m.PlannedReturnDate,
            m.Reason,
            m.InspectionSlot,
            m.RefundMethod,
            m.RefundAccount,
            m.Status == ReturnStatus.Completed
                && refunds.Any(d => d.UnitReturnId == m.UnitReturnId && d.Status == DepositRefundStatus.Pending)
                ? ReturnStatus.AwaitingPayout
                : m.Status,
            m.MoveOutDeadline,
            m.AccessLockedAt,
            inspections.Where(i => i.UnitReturnId == m.UnitReturnId)
                .OrderByDescending(i => i.ReturnInspectionId)
                .Select(i => new ReturnInspectionDto(
                    i.ReturnInspectionId, i.InspectedAt, i.ConditionResult, i.CleaningFee, i.RepairCharge,
                    i.OtherDeduction, i.RefundAmount, i.Status))
                .FirstOrDefault()));
    }
}
