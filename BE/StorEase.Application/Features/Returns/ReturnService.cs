using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.DepositRefunds;
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
    public async Task<Result<ReturnDto>> RequestAsync(CreateReturnRequest request, CancellationToken ct)
    {
        var created = await uow.InTransactionAsync<int>(async () =>
        {
            var contract = await uow.Repository<Contract>().FindAsync(request.ContractId, ct);
            // Someone else's contract is a 404, not a 403, so ids cannot be probed.
            if (contract is null || contract.CustomerId != currentUser.UserId)
                return Error.NotFound("Contract not found.");
            if (contract.Status != ContractStatus.Active)
                return Error.Conflict($"Only an Active contract can be handed back; this one is {contract.Status}.");

            var open = await uow.Repository<UnitReturn>().Query().AnyAsync(m =>
                m.ContractId == contract.ContractId &&
                (m.Status == ReturnStatus.Requested || m.Status == ReturnStatus.Approved ||
                 m.Status == ReturnStatus.Inspected), ct);
            if (open)
                return Error.Conflict("This contract already has an open return request.");

            // Rule 8 (SR-04): no notice period; an early return is allowed and costs a penalty at settlement.
            if (request.PlannedReturnDate < VietnamTime.Today)
                return Error.Validation("The return date cannot be in the past.");

            var unitReturn = new UnitReturn
            {
                ContractId = contract.ContractId,
                RequestedAt = DateTime.UtcNow,
                PlannedReturnDate = request.PlannedReturnDate,
                Reason = request.Reason,
                RefundMethod = request.RefundMethod,
                RefundAccount = request.RefundAccount,
                Status = ReturnStatus.Requested,
            };
            uow.Repository<UnitReturn>().Add(unitReturn);
            contract.Status = ContractStatus.PendingReturn;
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
            inspection.OtherDeduction += penalty;
            var (refund, shortfall) = DepositSettlement.Calculate(contract.DepositAmount, damage + penalty, outstanding);

            inspection.RefundAmount = refund;
            var reservation = (await uow.Repository<Reservation>().FindAsync(contract.ReservationId, ct))!;
            DepositRefundService.Owe(uow, reservation, refund, unitReturn);
            unitReturn.Status = ReturnStatus.Completed;
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
                unitReturn.UnitReturnId, contract.DepositAmount, damage, penalty, outstanding, refund, shortfall, unitReturn.Status);
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
            m.Status,
            inspections.Where(i => i.UnitReturnId == m.UnitReturnId)
                .OrderByDescending(i => i.ReturnInspectionId)
                .Select(i => new ReturnInspectionDto(
                    i.ReturnInspectionId, i.InspectedAt, i.ConditionResult, i.CleaningFee, i.RepairCharge,
                    i.OtherDeduction, i.RefundAmount, i.Status))
                .FirstOrDefault()));
    }
}
