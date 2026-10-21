using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Renewals.Dtos;
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
                .AnyAsync(r => r.ContractId == contract.ContractId && r.Status == RenewalStatus.Requested, ct);
            if (open)
                return Error.Conflict("This contract already has a renewal waiting for approval.");

            // Priced from the active policy; the contract keeps its signed rent until the renewal is approved.
            var price = await uow.Repository<UnitPrice>().Query()
                .Where(p => p.PricingPolicy.Status == PricingPolicyStatus.Active &&
                            p.UnitTypeId == contract.StorageUnit.UnitTypeId)
                .OrderByDescending(p => p.PricingPolicy.Version)
                .FirstOrDefaultAsync(ct);
            var newRent = price is null
                ? contract.MonthlyRent
                : RenewalPricing.Rent(price.BasePricePerMonth, request.ExtendMonths,
                    price.Discount3Month, price.Discount6Month, price.Discount12Month);

            var renewal = new Renewal
            {
                ContractId = contract.ContractId,
                RequestedAt = DateTime.UtcNow,
                ExtendMonths = request.ExtendMonths,
                NewEndDate = RenewalPricing.NewEndDate(contract.EndDate, request.ExtendMonths),
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
        var approved = await uow.InTransactionAsync<int>(async () =>
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

            renewal.Status = RenewalStatus.Approved;
            renewal.ApprovedBy = currentUser.UserId;
            contract.EndDate = renewal.NewEndDate;
            // shortcut: the new rent applies to every month not billed yet, not only the extension; split the rent per period if that matters.
            contract.MonthlyRent = renewal.NewMonthlyRent;
            // Codes expire the day after the end date (AccessCodeService), so the live one follows the new end date.
            foreach (var code in contract.AccessCodes.Where(a => a.IsActive))
                code.ExpiresAt = renewal.NewEndDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            return renewal.RenewalId;
        }, ct);
        return approved.IsSuccess ? await GetAsync(approved.Value, ct) : approved.Error;
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
        if (!currentUser.IsCustomer)
            return query;

        var customerId = currentUser.UserId;
        return query.Where(r => r.Contract.CustomerId == customerId);
    }

    private static IQueryable<RenewalDto> Project(IQueryable<Renewal> source) =>
        source.Select(r => new RenewalDto(
            r.RenewalId, r.ContractId, r.Contract.ContractNumber, r.Contract.StorageUnit.UnitCode, r.RequestedAt,
            r.ExtendMonths, r.Contract.EndDate, r.NewEndDate, r.Contract.MonthlyRent, r.NewMonthlyRent,
            r.Status, r.ApprovedBy));
}
