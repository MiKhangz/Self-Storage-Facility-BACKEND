using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.DepositRefunds;
using StorEase.Application.Features.Pricing;
using StorEase.Application.Features.Handovers.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Handovers;

public interface IHandoverService
{
    Task<Result<HandoverDto>> RequestAsync(CreateHandoverRequest request, CancellationToken ct);
    Task<Result<HandoverDto>> GetAsync(int id, CancellationToken ct);
    Task<Result<PagedResult<HandoverDto>>> ListAsync(HandoverStatus? status, int page, int pageSize, CancellationToken ct);
    Task<Result<HandoverDto>> ApproveAsync(int id, ApproveHandoverRequest request, CancellationToken ct);
    Task<Result<HandoverDto>> InspectAsync(int id, CreateHandoverInspectionRequest request, CancellationToken ct);
    Task<Result<DepositSettlementDto>> SettleAsync(int id, CancellationToken ct);
}

public class HandoverService(IUnitOfWork uow, ICurrentUser currentUser) : IHandoverService
{
    public async Task<Result<HandoverDto>> RequestAsync(CreateHandoverRequest request, CancellationToken ct)
    {
        var created = await uow.InTransactionAsync<int>(async () =>
        {
            var contract = await uow.Repository<Contract>().FindAsync(request.ContractId, ct);
            // Someone else's contract is a 404, not a 403, so ids cannot be probed.
            if (contract is null || contract.CustomerId != currentUser.UserId)
                return Error.NotFound("Contract not found.");
            if (contract.Status != ContractStatus.Active)
                return Error.Conflict($"Only an Active contract can be handed back; this one is {contract.Status}.");

            var open = await uow.Repository<Handover>().Query().AnyAsync(m =>
                m.ContractId == contract.ContractId &&
                (m.Status == HandoverStatus.Requested || m.Status == HandoverStatus.Approved ||
                 m.Status == HandoverStatus.Inspected), ct);
            if (open)
                return Error.Conflict("This contract already has an open handover request.");

            // Rule 8 (SR-04): no notice period; an early return is allowed and costs a penalty at settlement.
            if (request.PlannedHandoverDate < VietnamTime.Today)
                return Error.Validation("The handover date cannot be in the past.");

            var handover = new Handover
            {
                ContractId = contract.ContractId,
                RequestedAt = DateTime.UtcNow,
                PlannedHandoverDate = request.PlannedHandoverDate,
                Reason = request.Reason,
                RefundMethod = request.RefundMethod,
                RefundAccount = request.RefundAccount,
                Status = HandoverStatus.Requested,
            };
            uow.Repository<Handover>().Add(handover);
            contract.Status = ContractStatus.PendingHandover;
            await uow.SaveChangesAsync(ct);
            return handover.HandoverId;
        }, ct);

        return created.IsSuccess ? await GetAsync(created.Value, ct) : created.Error;
    }

    public async Task<Result<HandoverDto>> GetAsync(int id, CancellationToken ct)
    {
        var dto = await Project(Visible().Where(m => m.HandoverId == id)).FirstOrDefaultAsync(ct);
        return dto is null ? Error.NotFound("Handover request not found.") : dto;
    }

    /// <summary>The staff queue is <c>status=Approved</c>: nearest planned date first.</summary>
    public async Task<Result<PagedResult<HandoverDto>>> ListAsync(
        HandoverStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var query = Visible();
        if (status is not null)
            query = query.Where(m => m.Status == status);

        return await Project(query.OrderBy(m => m.PlannedHandoverDate).ThenBy(m => m.HandoverId))
            .ToPagedResultAsync(page, pageSize, ct);
    }

    public async Task<Result<HandoverDto>> ApproveAsync(int id, ApproveHandoverRequest request, CancellationToken ct)
    {
        var handover = await Visible().FirstOrDefaultAsync(m => m.HandoverId == id, ct);
        if (handover is null)
            return Error.NotFound("Handover request not found.");
        if (handover.Status != HandoverStatus.Requested)
            return Error.Conflict($"Only a Requested handover can be approved; this one is {handover.Status}.");

        handover.InspectionSlot = request.InspectionSlot;
        handover.Status = HandoverStatus.Approved;
        await uow.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<Result<HandoverDto>> InspectAsync(
        int id, CreateHandoverInspectionRequest request, CancellationToken ct)
    {
        var inspected = await uow.InTransactionAsync<int>(async () =>
        {
            var handover = await Visible().FirstOrDefaultAsync(m => m.HandoverId == id, ct);
            if (handover is null)
                return Error.NotFound("Handover request not found.");
            if (handover.Status != HandoverStatus.Approved)
                return Error.Conflict($"Only an Approved handover can be inspected; this one is {handover.Status}.");

            var contract = (await uow.Repository<Contract>().FindAsync(handover.ContractId, ct))!;
            uow.Repository<HandoverInspection>().Add(new HandoverInspection
            {
                HandoverId = handover.HandoverId,
                StorageUnitId = contract.StorageUnitId,
                StaffId = currentUser.UserId,
                InspectedAt = DateTime.UtcNow,
                ConditionResult = request.ConditionResult,
                CleaningFee = request.CleaningFee,
                RepairCharge = request.RepairCharge,
                OtherDeduction = request.OtherDeduction,
                RefundAmount = 0m, // set by settlement; the deposit is not refundable before then
                Status = HandoverInspectionStatus.Completed,
            });
            handover.Status = HandoverStatus.Inspected;
            return handover.HandoverId;
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
            var handover = await Visible().FirstOrDefaultAsync(m => m.HandoverId == id, ct);
            if (handover is null)
                return Error.NotFound("Handover request not found.");
            if (handover.Status != HandoverStatus.Inspected)
                return Error.Conflict($"A deposit is settled only after the handover inspection; this handover is {handover.Status}.");

            var inspection = await uow.Repository<HandoverInspection>().Query()
                .Where(i => i.HandoverId == id)
                .OrderByDescending(i => i.HandoverInspectionId)
                .FirstAsync(ct);
            if (inspection.Status != HandoverInspectionStatus.Completed)
                return Error.Conflict($"The handover inspection is {inspection.Status}; resolve it before settling.");

            var contract = (await uow.Repository<Contract>().FindAsync(handover.ContractId, ct))!;
            var outstanding = (await uow.Repository<Invoice>().Query()
                .Where(i => i.ContractId == contract.ContractId &&
                            (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.Overdue))
                .Select(i => i.TotalAmount - i.PaidAmount)
                .ToListAsync(ct)).Sum(); // a contract has a handful of invoices; summing here keeps SQLite (tests) working

            var damage = inspection.CleaningFee + inspection.RepairCharge + inspection.OtherDeduction;
            // The rate is the one the customer agreed to: the policy in force on the move-in date, like the rent (rule 4).
            var policy = await RentCalculator.PolicyForAsync(uow, contract.StartDate, ct);
            var penalty = DepositSettlement.EarlyTerminationPenalty(
                contract.DepositAmount, policy?.EarlyTerminationPercent ?? 0m, handover.PlannedHandoverDate, contract.EndDate);
            inspection.OtherDeduction += penalty;
            var (refund, shortfall) = DepositSettlement.Calculate(contract.DepositAmount, damage + penalty, outstanding);

            inspection.RefundAmount = refund;
            var reservation = (await uow.Repository<Reservation>().FindAsync(contract.ReservationId, ct))!;
            DepositRefundService.Owe(uow, reservation, refund, handover);
            handover.Status = HandoverStatus.Completed;
            contract.Status = ContractStatus.Closed;

            var codes = await uow.Repository<AccessCode>().Query()
                .Where(a => a.ContractId == contract.ContractId && a.IsActive).ToListAsync(ct);
            foreach (var code in codes)
                code.IsActive = false;

            // ponytail: a unit with major damage goes to Maintenance; staff flip it back through the unit board.
            var unit = (await uow.Repository<StorageUnit>().FindAsync(contract.StorageUnitId, ct))!;
            unit.Status = inspection.ConditionResult == ConditionResult.MajorDamage
                ? StorageUnitStatus.Maintenance
                : StorageUnitStatus.Available;

            return new DepositSettlementDto(
                handover.HandoverId, contract.DepositAmount, damage, penalty, outstanding, refund, shortfall, handover.Status);
        }, ct);

    /// <summary>Customers see their own; facility staff and managers the facilities they work at; admin and business ops all.</summary>
    private IQueryable<Handover> Visible()
    {
        var query = uow.Repository<Handover>().Query();
        var userId = currentUser.UserId;
        if (currentUser.IsCustomer)
            return query.Where(m => m.Contract.CustomerId == userId);
        if (StaffScope.SeesAllFacilities(currentUser.Role))
            return query;
        var facilityIds = StaffScope.FacilityIdsOf(uow, userId);
        return query.Where(m => facilityIds.Contains(m.Contract.StorageUnit.FacilityId));
    }

    private IQueryable<HandoverDto> Project(IQueryable<Handover> source)
    {
        var inspections = uow.Repository<HandoverInspection>().Query();
        return source.Select(m => new HandoverDto(
            m.HandoverId,
            m.ContractId,
            m.Contract.ContractNumber,
            m.Contract.CustomerId,
            m.Contract.Customer.FullName,
            m.Contract.StorageUnitId,
            m.Contract.StorageUnit.UnitCode,
            m.RequestedAt,
            m.PlannedHandoverDate,
            m.Reason,
            m.InspectionSlot,
            m.RefundMethod,
            m.RefundAccount,
            m.Status,
            inspections.Where(i => i.HandoverId == m.HandoverId)
                .OrderByDescending(i => i.HandoverInspectionId)
                .Select(i => new HandoverInspectionDto(
                    i.HandoverInspectionId, i.InspectedAt, i.ConditionResult, i.CleaningFee, i.RepairCharge,
                    i.OtherDeduction, i.RefundAmount, i.Status))
                .FirstOrDefault()));
    }
}
