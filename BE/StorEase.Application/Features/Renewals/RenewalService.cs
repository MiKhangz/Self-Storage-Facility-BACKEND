using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Pricing;
using StorEase.Application.Features.Renewals.Dtos;
using StorEase.Application.Features.Returns;
using StorEase.Application.Features.Returns.Dtos;
using StorEase.Application.Features.StorageUnits;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Renewals;

public interface IRenewalService
{
    Task<Result<RenewalDto>> RequestAsync(CreateRenewalRequest request, CancellationToken ct);
    Task<Result<RenewalDto>> GetAsync(int id, CancellationToken ct);
    Task<Result<PagedResult<RenewalDto>>> ListAsync(int? contractId, string? status, int page, int pageSize, CancellationToken ct);

    /// <summary>
    /// T-01: extends the contract to the renewal's end date at its new rent. The billing job then issues the extra
    /// months by itself, so no invoice is created here.
    /// </summary>
    Task<Result<RenewalDto>> ApproveAsync(int id, CancellationToken ct);

    /// <summary>T-01: the contract is left as it is. Also withdraws an offer of another unit the renter has not answered.</summary>
    Task<Result<RenewalDto>> RejectAsync(int id, CancellationToken ct);

    /// <summary>The renter agrees to move to the unit offered: the contract moves there and is extended.</summary>
    Task<Result<RenewalDto>> AcceptTransferAsync(int id, CancellationToken ct);

    /// <summary>The renter will not move: no renewal, and the unit is handed back on the end date (full deposit refund after inspection).</summary>
    Task<Result<RenewalDto>> DeclineTransferAsync(int id, CancellationToken ct);
}

public class RenewalService(IUnitOfWork uow, ICurrentUser currentUser) : IRenewalService
{
    public async Task<Result<RenewalDto>> RequestAsync(CreateRenewalRequest request, CancellationToken ct)
    {
        var created = await uow.InTransactionAsync<int>(async () =>
        {
            var contract = await uow.Repository<Contract>().Query()
                .Include(c => c.StorageUnit)
                .FirstOrDefaultAsync(c => c.ContractId == request.ContractId, ct);
            // Someone else's contract is a 404, not a 403, so ids cannot be probed.
            if (contract is null || contract.CustomerId != currentUser.UserId)
                return Error.NotFound("Contract not found.");
            if (contract.Status != ContractStatus.Active)
                return Error.Conflict($"Only an Active contract can be renewed; this one is {contract.Status}.");

            var open = await uow.Repository<Renewal>().Query()
                .AnyAsync(r => r.ContractId == contract.ContractId
                && (r.Status == RenewalStatus.Requested || r.Status == RenewalStatus.AwaitingCustomer), ct);
            if (open)
                return Error.Conflict("This contract already has a renewal waiting for approval.");

            // Priced from the active policy; the contract keeps its signed rent until the renewal is approved.
            var active = await uow.Repository<PricingPolicy>().Query()
                .Where(p => p.Status == PricingPolicyStatus.Active).OrderByDescending(p => p.Version)
                .Select(p => (int?)p.PricingPolicyId).FirstOrDefaultAsync(ct);
            // The facility's own price for the size, else the company price.
            var price = active is { } policyId
                ? await RentCalculator.PriceAtAsync(uow, policyId, contract.StorageUnit.FacilityId, contract.StorageUnit.UnitTypeId, VietnamTime.Today, ct)
                : null;
            var newRent = price is null
                ? contract.MonthlyRent
                : RenewalPricing.Rent(price.BasePricePerMonth, request.ExtendMonths,
                    price.Discount3Month, price.Discount6Month, price.Discount12Month);

            var renewal = new Renewal
            {
                ContractId = contract.ContractId,
                RequestedAt = DateTime.UtcNow,
                ExtendMonths = request.ExtendMonths,
                NewEndDate = RenewalPricing.NewEndDate(contract.StartDate, contract.EndDate, request.ExtendMonths),
                NewMonthlyRent = newRent,
                Status = RenewalStatus.Requested,
            };
            uow.Repository<Renewal>().Add(renewal);
            await uow.SaveChangesAsync(ct);
            return renewal.RenewalId;
        }, ct);

        return created.IsSuccess ? await GetAsync(created.Value, ct) : created.Error;
    }

    public async Task<Result<RenewalDto>> GetAsync(int id, CancellationToken ct)
    {
        var dto = await Project(Visible().Where(r => r.RenewalId == id)).FirstOrDefaultAsync(ct);
        return dto is null ? Error.NotFound("Renewal not found.") : dto;
    }

    public async Task<Result<PagedResult<RenewalDto>>> ListAsync(
        int? contractId, string? status, int page, int pageSize, CancellationToken ct)
    {
        var query = Visible();
        if (contractId is not null)
            query = query.Where(r => r.ContractId == contractId);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(r => r.Status == status);

        return await Project(query.OrderByDescending(r => r.RequestedAt).ThenByDescending(r => r.RenewalId))
            .ToPagedResultAsync(page, pageSize, ct);
    }

    public async Task<Result<RenewalDto>> ApproveAsync(int id, CancellationToken ct)
    {
        var approved = await uow.InExclusiveTransactionAsync<int>(async () =>
        {
            var renewal = await Managed().Include(r => r.Contract).ThenInclude(c => c.AccessCodes)
                .FirstOrDefaultAsync(r => r.RenewalId == id, ct);
            if (renewal is null)
                return Error.NotFound("Renewal not found.");
            if (renewal.Status != RenewalStatus.Requested)
                return Error.Conflict($"Only a Requested renewal can be approved; this one is {renewal.Status}.");
            var contract = renewal.Contract;
            if (contract.Status != ContractStatus.Active)
                return Error.Conflict($"Only an Active contract can be renewed; this one is {contract.Status}.");
            renewal.ApprovedBy = currentUser.UserId;

            // The next customer booked this unit after the end date: offer the renter a free unit of the same size instead.
            if (!await UnitSchedule.IsFreeAsync(uow, contract.StorageUnitId, contract.EndDate.AddDays(1), renewal.NewEndDate, ct,
                    exceptReservationId: contract.ReservationId, exceptContractId: contract.ContractId))
            {
                var unit = (await uow.Repository<StorageUnit>().FindAsync(contract.StorageUnitId, ct))!;
                if (contract.EndDate < VietnamTime.Today)
                    return Error.Conflict($"The rental has ended and the next customer booked unit {unit.UnitCode}; reject the renewal.");
                var free = await UnitSchedule.FreeUnitsAsync(uow, unit.FacilityId, unit.UnitTypeId, VietnamTime.Today, renewal.NewEndDate, ct);
                // Free for the period is not enough to move onto: a draft for a later rental already claims its unit.
                var freeIds = free.Select(u => u.StorageUnitId).ToList();
                var claimed = await UnitSchedule.HoldingContracts(uow).Where(c => freeIds.Contains(c.StorageUnitId))
                    .Select(c => c.StorageUnitId).ToListAsync(ct);
                free.RemoveAll(u => claimed.Contains(u.StorageUnitId));
                if (free.Count == 0)
                    return Error.Conflict($"Unit {unit.UnitCode} is booked by the next customer and no other unit of this size is free; reject the renewal.");
                renewal.TransferStorageUnitId = free[0].StorageUnitId;
                renewal.Status = RenewalStatus.AwaitingCustomer;
                await uow.SaveChangesAsync(ct);
                return renewal.RenewalId;
            }
            Extend(renewal, contract);
            return renewal.RenewalId;
        }, ct);
        return approved.IsSuccess ? await GetAsync(approved.Value, ct) : approved.Error;
    }

    public async Task<Result<RenewalDto>> AcceptTransferAsync(int id, CancellationToken ct)
    {
        var accepted = await uow.InExclusiveTransactionAsync<int>(async () =>
        {
            var renewal = await Own().Include(r => r.Contract).ThenInclude(c => c.AccessCodes)
                .Include(r => r.Contract).ThenInclude(c => c.StorageUnit)
                .FirstOrDefaultAsync(r => r.RenewalId == id, ct);
            if (renewal is null)
                return Error.NotFound("Renewal not found.");
            if (renewal.Status != RenewalStatus.AwaitingCustomer)
                return Error.Conflict($"No unit change is waiting for your answer; this renewal is {renewal.Status}.");
            var contract = renewal.Contract;
            if (contract.Status != ContractStatus.Active)
                return Error.Conflict($"Only an Active contract can be renewed; this one is {contract.Status}.");
            if (contract.EndDate < VietnamTime.Today)
                return Error.Conflict("The rental has ended, so the offer of another unit is closed.");
            var target = (await uow.Repository<StorageUnit>().FindAsync(renewal.TransferStorageUnitId!.Value, ct))!;
            if (await UnitSchedule.HoldingContracts(uow).AnyAsync(c => c.StorageUnitId == target.StorageUnitId, ct)
                || !await UnitSchedule.IsFreeAsync(uow, target.StorageUnitId, VietnamTime.Today, renewal.NewEndDate, ct, exceptContractId: contract.ContractId))
                return Error.Conflict($"Unit {target.UnitCode} was taken meanwhile; staff will offer another.");

            // The contract moves now so the old unit is ready for the next customer; access codes belong to the contract and carry over.
            var old = contract.StorageUnit;
            contract.StorageUnitId = target.StorageUnitId;
            contract.StorageUnit = target;
            target.Status = StorageUnitStatus.Occupied;
            Extend(renewal, contract);
            await uow.SaveChangesAsync(ct);
            await UnitSchedule.ReleaseAsync(uow, old, ct);
            await uow.SaveChangesAsync(ct);
            return renewal.RenewalId;
        }, ct);
        return accepted.IsSuccess ? await GetAsync(accepted.Value, ct) : accepted.Error;
    }

    public async Task<Result<RenewalDto>> DeclineTransferAsync(int id, CancellationToken ct)
    {
        var renewal = await Own().Include(r => r.Contract).FirstOrDefaultAsync(r => r.RenewalId == id, ct);
        if (renewal is null)
            return Error.NotFound("Renewal not found.");
        if (renewal.Status != RenewalStatus.AwaitingCustomer)
            return Error.Conflict($"No unit change is waiting for your answer; this renewal is {renewal.Status}.");
        // Hand back on the end date: no early-return penalty, so the deposit comes back in full after inspection.
        // Once that date has passed it is a normal hand-back from today.
        var today = VietnamTime.Today;
        var ended = renewal.Contract.EndDate < today;
        var handBack = await new ReturnService(uow, currentUser).RequestAsync(
            new CreateReturnRequest(renewal.ContractId, ended ? today : renewal.Contract.EndDate, "Không gia hạn: kho đã có khách đặt tiếp", null, null),
            atEndDate: !ended, ct);
        if (!handBack.IsSuccess)
            return handBack.Error;
        renewal.Status = RenewalStatus.Rejected;
        await uow.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private static void Extend(Renewal renewal, Contract contract)
    {
        renewal.Status = RenewalStatus.Approved;
        contract.EndDate = renewal.NewEndDate;
        // shortcut: the new rent applies to every month not billed yet, not only the extension; split the rent per period if that matters.
        contract.MonthlyRent = renewal.NewMonthlyRent;
        // Codes expire the day after the end date (AccessCodeService), so the live one follows the new end date.
        foreach (var code in contract.AccessCodes.Where(a => a.IsActive))
            code.ExpiresAt = renewal.NewEndDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    }

    /// <summary>The signed-in customer's own renewals.</summary>
    private IQueryable<Renewal> Own()
    {
        var customerId = currentUser.UserId;
        return uow.Repository<Renewal>().Query().Where(r => r.Contract.CustomerId == customerId);
    }

    public async Task<Result<RenewalDto>> RejectAsync(int id, CancellationToken ct)
    {
        var renewal = await Managed().FirstOrDefaultAsync(r => r.RenewalId == id, ct);
        if (renewal is null)
            return Error.NotFound("Renewal not found.");
        if (renewal.Status is not (RenewalStatus.Requested or RenewalStatus.AwaitingCustomer))
            return Error.Conflict($"Only a renewal still waiting can be rejected; this one is {renewal.Status}.");
        renewal.Status = RenewalStatus.Rejected;
        renewal.ApprovedBy = currentUser.UserId;
        await uow.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Renewals a manager may decide: their own facilities; business operations and admin see all.</summary>
    private IQueryable<Renewal> Managed()
    {
        var query = uow.Repository<Renewal>().Query();
        if (StaffScope.SeesAllFacilities(currentUser.Role))
            return query;
        var facilityIds = StaffScope.FacilityIdsOf(uow, currentUser.UserId);
        return query.Where(r => facilityIds.Contains(r.Contract.StorageUnit.FacilityId));
    }

    private IQueryable<Renewal> Visible()
    {
        var query = uow.Repository<Renewal>().Query();
        if (StaffScope.SeesAllFacilities(currentUser.Role))
            return query;
        if (!currentUser.IsCustomer)
        {
            var facilityIds = StaffScope.FacilityIdsOf(uow, currentUser.UserId);
            return query.Where(r => facilityIds.Contains(r.Contract.StorageUnit.FacilityId));
        }

        var customerId = currentUser.UserId;
        return query.Where(r => r.Contract.CustomerId == customerId);
    }

    private static IQueryable<RenewalDto> Project(IQueryable<Renewal> source) =>
        source.Select(r => new RenewalDto(
            r.RenewalId, r.ContractId, r.Contract.ContractNumber, r.Contract.StorageUnit.UnitCode, r.RequestedAt,
            r.ExtendMonths, r.Contract.EndDate, r.NewEndDate, r.Contract.MonthlyRent, r.NewMonthlyRent,
            r.Status, r.ApprovedBy, r.TransferStorageUnit != null ? r.TransferStorageUnit.UnitCode : null));
}
