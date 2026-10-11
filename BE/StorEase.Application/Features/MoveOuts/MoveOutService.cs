using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.MoveOuts.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.MoveOuts;

public interface IMoveOutService
{
    Task<Result<MoveOutRequestDto>> RequestAsync(CreateMoveOutRequest request, CancellationToken ct);
    Task<Result<MoveOutRequestDto>> GetAsync(int id, CancellationToken ct);
    Task<Result<PagedResult<MoveOutRequestDto>>> ListAsync(MoveOutRequestStatus? status, int page, int pageSize, CancellationToken ct);
    Task<Result<MoveOutRequestDto>> ApproveAsync(int id, ApproveMoveOutRequest request, CancellationToken ct);
    Task<Result<MoveOutRequestDto>> InspectAsync(int id, CreateReturnInspectionRequest request, CancellationToken ct);
    Task<Result<DepositSettlementDto>> SettleAsync(int id, CancellationToken ct);
}

public class MoveOutService(IUnitOfWork uow, ICurrentUser currentUser) : IMoveOutService
{
    private const int DefaultNoticeDays = 30;

    public async Task<Result<MoveOutRequestDto>> RequestAsync(CreateMoveOutRequest request, CancellationToken ct)
    {
        var created = await uow.InTransactionAsync<int>(async () =>
        {
            var contract = await uow.Repository<Contract>().FindAsync(request.ContractId, ct);
            // Someone else's contract is a 404, not a 403, so ids cannot be probed.
            if (contract is null || contract.CustomerId != currentUser.UserId)
                return Error.NotFound("Contract not found.");
            if (contract.Status != ContractStatus.Active)
                return Error.Conflict($"Only an Active contract can be moved out of; this one is {contract.Status}.");

            var open = await uow.Repository<MoveOutRequest>().Query().AnyAsync(m =>
                m.ContractId == contract.ContractId &&
                (m.Status == MoveOutRequestStatus.Requested || m.Status == MoveOutRequestStatus.Approved ||
                 m.Status == MoveOutRequestStatus.Inspected), ct);
            if (open)
                return Error.Conflict("This contract already has an open move-out request.");

            // Business rule 8: the notice period comes from the active pricing policy.
            var noticeDays = await uow.Repository<PricingPolicy>().Query()
                .Where(p => p.Status == PricingPolicyStatus.Active)
                .OrderByDescending(p => p.Version)
                .Select(p => (int?)p.NoticeDays)
                .FirstOrDefaultAsync(ct) ?? DefaultNoticeDays;
            var earliest = DepositSettlement.EarliestMoveOutDate(DateOnly.FromDateTime(DateTime.UtcNow), noticeDays);
            if (request.PlannedMoveOutDate < earliest)
                return Error.Conflict($"Move-out needs {noticeDays} days notice; the earliest date is {earliest:yyyy-MM-dd}.");

            var moveOut = new MoveOutRequest
            {
                ContractId = contract.ContractId,
                RequestedAt = DateTime.UtcNow,
                PlannedMoveOutDate = request.PlannedMoveOutDate,
                Reason = request.Reason,
                RefundMethod = request.RefundMethod,
                RefundAccount = request.RefundAccount,
                Status = MoveOutRequestStatus.Requested,
            };
            uow.Repository<MoveOutRequest>().Add(moveOut);
            contract.Status = ContractStatus.PendingMoveOut;
            await uow.SaveChangesAsync(ct);
            return moveOut.MoveOutRequestId;
        }, ct);

        return created.IsSuccess ? await GetAsync(created.Value, ct) : created.Error;
    }

    public async Task<Result<MoveOutRequestDto>> GetAsync(int id, CancellationToken ct)
    {
        var dto = await Project(Visible().Where(m => m.MoveOutRequestId == id)).FirstOrDefaultAsync(ct);
        return dto is null ? Error.NotFound("Move-out request not found.") : dto;
    }

    /// <summary>The staff queue is <c>status=Approved</c>: nearest planned date first.</summary>
    public async Task<Result<PagedResult<MoveOutRequestDto>>> ListAsync(
        MoveOutRequestStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var query = Visible();
        if (status is not null)
            query = query.Where(m => m.Status == status);

        return await Project(query.OrderBy(m => m.PlannedMoveOutDate).ThenBy(m => m.MoveOutRequestId))
            .ToPagedResultAsync(page, pageSize, ct);
    }

    public async Task<Result<MoveOutRequestDto>> ApproveAsync(int id, ApproveMoveOutRequest request, CancellationToken ct)
    {
        var moveOut = await uow.Repository<MoveOutRequest>().FindAsync(id, ct);
        if (moveOut is null)
            return Error.NotFound("Move-out request not found.");
        if (moveOut.Status != MoveOutRequestStatus.Requested)
            return Error.Conflict($"Only a Requested move-out can be approved; this one is {moveOut.Status}.");

        moveOut.InspectionSlot = request.InspectionSlot;
        moveOut.Status = MoveOutRequestStatus.Approved;
        await uow.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<Result<MoveOutRequestDto>> InspectAsync(
        int id, CreateReturnInspectionRequest request, CancellationToken ct)
    {
        var inspected = await uow.InTransactionAsync<int>(async () =>
        {
            var moveOut = await uow.Repository<MoveOutRequest>().FindAsync(id, ct);
            if (moveOut is null)
                return Error.NotFound("Move-out request not found.");
            if (moveOut.Status != MoveOutRequestStatus.Approved)
                return Error.Conflict($"Only an Approved move-out can be inspected; this one is {moveOut.Status}.");

            var contract = (await uow.Repository<Contract>().FindAsync(moveOut.ContractId, ct))!;
            uow.Repository<ReturnInspection>().Add(new ReturnInspection
            {
                MoveOutRequestId = moveOut.MoveOutRequestId,
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
            moveOut.Status = MoveOutRequestStatus.Inspected;
            return moveOut.MoveOutRequestId;
        }, ct);

        return inspected.IsSuccess ? await GetAsync(inspected.Value, ct) : inspected.Error;
    }

    /// <summary>
    /// Business rule 7. Records the refund and closes the contract; the payout itself goes through the
    /// payment gateway, which is not connected, so nothing is paid out here.
    /// </summary>
    public Task<Result<DepositSettlementDto>> SettleAsync(int id, CancellationToken ct) =>
        uow.InTransactionAsync<DepositSettlementDto>(async () =>
        {
            var moveOut = await uow.Repository<MoveOutRequest>().FindAsync(id, ct);
            if (moveOut is null)
                return Error.NotFound("Move-out request not found.");
            if (moveOut.Status != MoveOutRequestStatus.Inspected)
                return Error.Conflict($"A deposit is settled only after the return inspection; this move-out is {moveOut.Status}.");

            var inspection = await uow.Repository<ReturnInspection>().Query()
                .Where(i => i.MoveOutRequestId == id)
                .OrderByDescending(i => i.ReturnInspectionId)
                .FirstAsync(ct);
            if (inspection.Status != ReturnInspectionStatus.Completed)
                return Error.Conflict($"The return inspection is {inspection.Status}; resolve it before settling.");

            var contract = (await uow.Repository<Contract>().FindAsync(moveOut.ContractId, ct))!;
            var outstanding = await uow.Repository<Invoice>().Query()
                .Where(i => i.ContractId == contract.ContractId &&
                            (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.Overdue))
                .SumAsync(i => i.TotalAmount - i.PaidAmount, ct);

            var damage = inspection.CleaningFee + inspection.RepairCharge + inspection.OtherDeduction;
            var (refund, shortfall) = DepositSettlement.Calculate(contract.DepositAmount, damage, outstanding);

            inspection.RefundAmount = refund;
            moveOut.Status = MoveOutRequestStatus.Completed;
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
                moveOut.MoveOutRequestId, contract.DepositAmount, damage, outstanding, refund, shortfall, moveOut.Status);
        }, ct);

    private IQueryable<MoveOutRequest> Visible()
    {
        var query = uow.Repository<MoveOutRequest>().Query();
        if (!currentUser.IsCustomer)
            return query;

        var customerId = currentUser.UserId;
        return query.Where(m => m.Contract.CustomerId == customerId);
    }

    private IQueryable<MoveOutRequestDto> Project(IQueryable<MoveOutRequest> source)
    {
        var inspections = uow.Repository<ReturnInspection>().Query();
        return source.Select(m => new MoveOutRequestDto(
            m.MoveOutRequestId,
            m.ContractId,
            m.Contract.ContractNumber,
            m.Contract.CustomerId,
            m.Contract.Customer.FullName,
            m.Contract.StorageUnitId,
            m.Contract.StorageUnit.UnitCode,
            m.RequestedAt,
            m.PlannedMoveOutDate,
            m.Reason,
            m.InspectionSlot,
            m.RefundMethod,
            m.RefundAccount,
            m.Status,
            inspections.Where(i => i.MoveOutRequestId == m.MoveOutRequestId)
                .OrderByDescending(i => i.ReturnInspectionId)
                .Select(i => new ReturnInspectionDto(
                    i.ReturnInspectionId, i.InspectedAt, i.ConditionResult, i.CleaningFee, i.RepairCharge,
                    i.OtherDeduction, i.RefundAmount, i.Status))
                .FirstOrDefault()));
    }
}
