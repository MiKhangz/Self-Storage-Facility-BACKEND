using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Pricing.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Pricing;

public interface IPricingService
{
    Task<Result<PagedResult<PricingPolicyDto>>> ListPoliciesAsync(int page, int pageSize, CancellationToken ct);
    Task<Result<PricingPolicyDto>> GetPolicyAsync(int policyId, CancellationToken ct);
    Task<Result<PricingPolicyDto>> CreatePolicyAsync(CreatePricingPolicyRequest request, CancellationToken ct);
    Task<Result<PricingPolicyDto>> ActivatePolicyAsync(int policyId, CancellationToken ct);
    Task<Result<UnitPriceDto>> UpsertUnitPriceAsync(int policyId, int unitTypeId, UpsertUnitPriceRequest request, CancellationToken ct);
}

public sealed class PricingService(IUnitOfWork uow, ICurrentUser currentUser) : IPricingService
{
    public async Task<Result<PagedResult<PricingPolicyDto>>> ListPoliciesAsync(int page, int pageSize, CancellationToken ct)
    {
        var paged = await WithPrices().OrderByDescending(p => p.Version).ToPagedResultAsync(page, pageSize, ct);
        return new PagedResult<PricingPolicyDto>(paged.Items.Select(ToDto).ToList(), paged.Page, paged.PageSize, paged.TotalCount);
    }

    public async Task<Result<PricingPolicyDto>> GetPolicyAsync(int policyId, CancellationToken ct)
    {
        var policy = await WithPrices().FirstOrDefaultAsync(p => p.PricingPolicyId == policyId, ct);
        return policy is null ? Error.NotFound("Pricing policy not found.") : ToDto(policy);
    }

    public async Task<Result<PricingPolicyDto>> CreatePolicyAsync(CreatePricingPolicyRequest request, CancellationToken ct)
    {
        // An open-ended active policy is intentionally allowed to coexist with a future Draft;
        // activation closes the old version on the day before the new one starts.
        var newEnd = request.EffectiveTo ?? DateOnly.MaxValue;
        var overlap = await uow.Repository<PricingPolicy>().Query().AnyAsync(p =>
            p.EffectiveTo != null
            && p.EffectiveFrom <= newEnd
            && p.EffectiveTo >= request.EffectiveFrom, ct);
        if (overlap)
            return Error.Conflict("The effective date range overlaps an existing pricing policy.");

        var version = (await uow.Repository<PricingPolicy>().Query().MaxAsync(p => (int?)p.Version, ct) ?? 0) + 1;
        var policy = new PricingPolicy
        {
            CreatedBy = currentUser.UserId,
            Version = version,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            DepositMonths = request.DepositMonths,
            GraceDays = request.GraceDays,
            NoticeDays = request.NoticeDays,
            LateFeePercentPerWeek = request.LateFeePercentPerWeek,
            SuspendAfterDays = request.SuspendAfterDays,
            ClearOutAfterDays = request.ClearOutAfterDays,
            CancelFreeHours = request.CancelFreeHours,
            Status = PricingPolicyStatus.Draft,
        };
        uow.Repository<PricingPolicy>().Add(policy);
        await uow.SaveChangesAsync(ct);
        return await GetPolicyAsync(policy.PricingPolicyId, ct);
    }

    public async Task<Result<PricingPolicyDto>> ActivatePolicyAsync(int policyId, CancellationToken ct)
    {
        var activated = await uow.InTransactionAsync<int>(async () =>
        {
            var policy = await uow.Repository<PricingPolicy>().FindAsync(policyId, ct);
            if (policy is null)
                return Error.NotFound("Pricing policy not found.");
            // Activating early would supersede the current policy before this one starts, leaving a gap with no price.
            if (policy.EffectiveFrom > VietnamTime.Today)
                return Error.Conflict($"This policy starts on {policy.EffectiveFrom:yyyy-MM-dd}; activate it on or after that date.");
            var futureActive = await uow.Repository<PricingPolicy>().Query().AnyAsync(p =>
                p.Status == PricingPolicyStatus.Active
                && p.PricingPolicyId != policyId
                && p.EffectiveFrom >= policy.EffectiveFrom, ct);
            if (futureActive)
                return Error.Conflict("A later active pricing policy already exists.");
            if (!await uow.Repository<UnitPrice>().Query().AnyAsync(p => p.PricingPolicyId == policyId, ct))
                return Error.Conflict("A pricing policy must have at least one unit price before activation.");

            var active = await uow.Repository<PricingPolicy>().Query()
                .Where(p => p.Status == PricingPolicyStatus.Active && p.PricingPolicyId != policyId)
                .ToListAsync(ct);
            foreach (var old in active)
            {
                old.Status = PricingPolicyStatus.Superseded;
                if (old.EffectiveTo is null || old.EffectiveTo >= policy.EffectiveFrom)
                    old.EffectiveTo = policy.EffectiveFrom.AddDays(-1);
            }
            policy.Status = PricingPolicyStatus.Active;
            return policy.PricingPolicyId;
        }, ct);
        return activated.IsSuccess ? await GetPolicyAsync(activated.Value, ct) : activated.Error;
    }

    public async Task<Result<UnitPriceDto>> UpsertUnitPriceAsync(
        int policyId, int unitTypeId, UpsertUnitPriceRequest request, CancellationToken ct)
    {
        var policy = await uow.Repository<PricingPolicy>().FindAsync(policyId, ct);
        if (policy is null)
            return Error.NotFound("Pricing policy not found.");
        if (policy.Status != PricingPolicyStatus.Draft)
            return Error.Conflict("Only a Draft pricing policy can be edited.");
        var type = await uow.Repository<UnitType>().Query().FirstOrDefaultAsync(t => t.UnitTypeId == unitTypeId, ct);
        if (type is null)
            return Error.NotFound("Unit type not found.");

        var price = await uow.Repository<UnitPrice>().Query()
            .FirstOrDefaultAsync(p => p.PricingPolicyId == policyId && p.UnitTypeId == unitTypeId, ct);
        if (price is null)
        {
            price = new UnitPrice { PricingPolicyId = policyId, UnitTypeId = unitTypeId };
            uow.Repository<UnitPrice>().Add(price);
        }
        price.BasePricePerMonth = request.BasePricePerMonth;
        price.Discount3Month = request.Discount3Month;
        price.Discount6Month = request.Discount6Month;
        price.Discount12Month = request.Discount12Month;
        await uow.SaveChangesAsync(ct);
        return new UnitPriceDto(price.UnitPriceId, policyId, unitTypeId, type.Code, type.Name,
            price.BasePricePerMonth, price.Discount3Month, price.Discount6Month, price.Discount12Month);
    }

    private IQueryable<PricingPolicy> WithPrices() => uow.Repository<PricingPolicy>().Query().AsNoTracking()
        .Include(p => p.UnitPrices).ThenInclude(u => u.UnitType)
        .Include(p => p.FeeTypes);

    private static PricingPolicyDto ToDto(PricingPolicy policy) => new(
        policy.PricingPolicyId, policy.Version, policy.EffectiveFrom, policy.EffectiveTo,
        policy.DepositMonths, policy.GraceDays, policy.NoticeDays, policy.LateFeePercentPerWeek,
        policy.SuspendAfterDays, policy.ClearOutAfterDays, policy.CancelFreeHours, policy.Status,
        policy.UnitPrices.OrderBy(p => p.UnitType.Code)
            .Select(p => new UnitPriceDto(p.UnitPriceId, p.PricingPolicyId, p.UnitTypeId, p.UnitType.Code, p.UnitType.Name,
                p.BasePricePerMonth, p.Discount3Month, p.Discount6Month, p.Discount12Month)).ToList(),
        policy.FeeTypes.OrderBy(f => f.Code)
            .Select(f => new FeeTypeDto(f.FeeTypeId, f.PricingPolicyId, f.Code, f.Name, f.Amount, f.IsPercentage)).ToList());
}
