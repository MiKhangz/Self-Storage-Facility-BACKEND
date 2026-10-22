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
    Task<Result<PolicyTermsDto>> GetCurrentTermsAsync(CancellationToken ct);
    Task<Result<PricingPolicyDto>> CreatePolicyAsync(CreatePricingPolicyRequest request, CancellationToken ct);
    Task<Result<PricingPolicyDto>> ActivatePolicyAsync(int policyId, CancellationToken ct);
    /// <summary>
    /// A new Draft with the prices, discounts, fees and terms of any version, starting today. This is how an old price is
    /// brought back: as a new version, so no past dates change. 409 while another Draft exists.
    /// </summary>
    Task<Result<PricingPolicyDto>> CopyToDraftAsync(int policyId, CancellationToken ct);
    /// <summary>T-03: changes the dates and terms of a Draft policy; an Active or Expired one is history and stays.</summary>
    Task<Result<PricingPolicyDto>> UpdatePolicyAsync(int policyId, UpdatePricingPolicyRequest request, CancellationToken ct);
    Task<Result<UnitPriceDto>> UpsertUnitPriceAsync(int policyId, int unitTypeId, UpsertUnitPriceRequest request, CancellationToken ct);
    Task<Result<FeeTypeDto>> CreateFeeTypeAsync(int policyId, FeeTypeRequest request, CancellationToken ct);
    Task<Result<FeeTypeDto>> UpdateFeeTypeAsync(int feeTypeId, FeeTypeRequest request, CancellationToken ct);
    Task<Result<bool>> DeleteFeeTypeAsync(int feeTypeId, CancellationToken ct);
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

    public async Task<Result<PolicyTermsDto>> GetCurrentTermsAsync(CancellationToken ct)
    {
        var policy = await RentCalculator.PolicyForAsync(uow, VietnamTime.Today, ct);
        if (policy is null)
            return Error.NotFound("No pricing policy is in force today.");
        var discounts = await uow.Repository<UnitPrice>().Query()
            .Where(p => p.PricingPolicyId == policy.PricingPolicyId)
            .OrderBy(p => p.UnitTypeId)
            .Select(p => new SizeDiscountDto(p.UnitTypeId, p.Discount3Month, p.Discount6Month, p.Discount12Month))
            .ToListAsync(ct);
        return new PolicyTermsDto(policy.PricingPolicyId, policy.Version, policy.DepositMonths, policy.GraceDays,
            policy.LateFeePercentPerWeek, policy.EarlyTerminationPercent, policy.SuspendAfterDays, policy.ClearOutAfterDays,
            policy.CancelFreeHours, discounts);
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
            EarlyTerminationPercent = request.EarlyTerminationPercent,
            SuspendAfterDays = request.SuspendAfterDays,
            ClearOutAfterDays = request.ClearOutAfterDays,
            CancelFreeHours = request.CancelFreeHours,
            Status = PricingPolicyStatus.Draft,
        };
        uow.Repository<PricingPolicy>().Add(policy);
        await uow.SaveChangesAsync(ct);
        return await GetPolicyAsync(policy.PricingPolicyId, ct);
    }

    public async Task<Result<PricingPolicyDto>> UpdatePolicyAsync(int policyId, UpdatePricingPolicyRequest request, CancellationToken ct)
    {
        var policy = await uow.Repository<PricingPolicy>().FindAsync(policyId, ct);
        if (policy is null)
            return Error.NotFound("Pricing policy not found.");
        if (policy.Status != PricingPolicyStatus.Draft)
            return Error.Conflict($"Only a Draft policy can be edited; this one is {policy.Status}.");
        // Same overlap rule as CreatePolicyAsync, ignoring the policy itself.
        var newEnd = request.EffectiveTo ?? DateOnly.MaxValue;
        var overlap = await uow.Repository<PricingPolicy>().Query().AnyAsync(p =>
            p.PricingPolicyId != policyId
            && p.EffectiveTo != null
            && p.EffectiveFrom <= newEnd
            && p.EffectiveTo >= request.EffectiveFrom, ct);
        if (overlap)
            return Error.Conflict("The effective date range overlaps an existing pricing policy.");

        policy.EffectiveFrom = request.EffectiveFrom;
        policy.EffectiveTo = request.EffectiveTo;
        policy.DepositMonths = request.DepositMonths;
        policy.GraceDays = request.GraceDays;
        policy.NoticeDays = request.NoticeDays;
        policy.LateFeePercentPerWeek = request.LateFeePercentPerWeek;
        policy.EarlyTerminationPercent = request.EarlyTerminationPercent;
        policy.SuspendAfterDays = request.SuspendAfterDays;
        policy.ClearOutAfterDays = request.ClearOutAfterDays;
        policy.CancelFreeHours = request.CancelFreeHours;
        await uow.SaveChangesAsync(ct);
        return await GetPolicyAsync(policyId, ct);
    }

    public async Task<Result<PricingPolicyDto>> CopyToDraftAsync(int policyId, CancellationToken ct)
    {
        var source = await uow.Repository<PricingPolicy>().Query().AsNoTracking()
            .Include(p => p.UnitPrices).Include(p => p.FeeTypes)
            .FirstOrDefaultAsync(p => p.PricingPolicyId == policyId, ct);
        if (source is null)
            return Error.NotFound("Pricing policy not found.");
        // The page works on one draft at a time; a second one would hide which is meant to be published.
        if (await uow.Repository<PricingPolicy>().Query().AnyAsync(p => p.Status == PricingPolicyStatus.Draft, ct))
            return Error.Conflict("Publish or delete the current draft first.");

        var version = (await uow.Repository<PricingPolicy>().Query().MaxAsync(p => (int?)p.Version, ct) ?? 0) + 1;
        var draft = new PricingPolicy
        {
            CreatedBy = currentUser.UserId,
            Version = version,
            EffectiveFrom = VietnamTime.Today,
            DepositMonths = source.DepositMonths,
            GraceDays = source.GraceDays,
            NoticeDays = source.NoticeDays,
            LateFeePercentPerWeek = source.LateFeePercentPerWeek,
            EarlyTerminationPercent = source.EarlyTerminationPercent,
            SuspendAfterDays = source.SuspendAfterDays,
            ClearOutAfterDays = source.ClearOutAfterDays,
            CancelFreeHours = source.CancelFreeHours,
            Status = PricingPolicyStatus.Draft,
        };
        foreach (var price in source.UnitPrices)
            draft.UnitPrices.Add(new UnitPrice
            {
                UnitTypeId = price.UnitTypeId, BasePricePerMonth = price.BasePricePerMonth,
                Discount3Month = price.Discount3Month, Discount6Month = price.Discount6Month, Discount12Month = price.Discount12Month,
            });
        foreach (var fee in source.FeeTypes)
            draft.FeeTypes.Add(new FeeType { Code = fee.Code, Name = fee.Name, Amount = fee.Amount, IsPercentage = fee.IsPercentage });
        uow.Repository<PricingPolicy>().Add(draft);
        await uow.SaveChangesAsync(ct);
        return await GetPolicyAsync(draft.PricingPolicyId, ct);
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

    public async Task<Result<FeeTypeDto>> CreateFeeTypeAsync(int policyId, FeeTypeRequest request, CancellationToken ct)
    {
        var policy = await uow.Repository<PricingPolicy>().FindAsync(policyId, ct);
        if (policy is null)
            return Error.NotFound("Pricing policy not found.");
        var fee = new FeeType { PricingPolicyId = policyId };
        var applied = await ApplyFeeAsync(policy, fee, request, ct);
        if (!applied.IsSuccess)
            return applied.Error;
        uow.Repository<FeeType>().Add(fee);
        await uow.SaveChangesAsync(ct);
        return ToDto(fee);
    }

    public async Task<Result<FeeTypeDto>> UpdateFeeTypeAsync(int feeTypeId, FeeTypeRequest request, CancellationToken ct)
    {
        var fee = await uow.Repository<FeeType>().Query().Include(f => f.PricingPolicy)
            .FirstOrDefaultAsync(f => f.FeeTypeId == feeTypeId, ct);
        if (fee is null)
            return Error.NotFound("Fee type not found.");
        var applied = await ApplyFeeAsync(fee.PricingPolicy, fee, request, ct);
        if (!applied.IsSuccess)
            return applied.Error;
        await uow.SaveChangesAsync(ct);
        return ToDto(fee);
    }

    public async Task<Result<bool>> DeleteFeeTypeAsync(int feeTypeId, CancellationToken ct)
    {
        var fee = await uow.Repository<FeeType>().Query().Include(f => f.PricingPolicy)
            .FirstOrDefaultAsync(f => f.FeeTypeId == feeTypeId, ct);
        if (fee is null)
            return Error.NotFound("Fee type not found.");
        if (fee.PricingPolicy.Status != PricingPolicyStatus.Draft)
            return Error.Conflict("Only a Draft pricing policy can be edited.");
        if (await uow.Repository<InvoiceItem>().Query().AnyAsync(i => i.FeeTypeId == feeTypeId, ct))
            return Error.Conflict("This fee has already been billed and cannot be deleted.");
        uow.Repository<FeeType>().Remove(fee);
        await uow.SaveChangesAsync(ct);
        return true;
    }

    private async Task<Result<bool>> ApplyFeeAsync(PricingPolicy policy, FeeType fee, FeeTypeRequest request, CancellationToken ct)
    {
        // Fees of an active policy are already quoted to customers, so they change only through a new version.
        if (policy.Status != PricingPolicyStatus.Draft)
            return Error.Conflict("Only a Draft pricing policy can be edited.");
        var code = request.Code.Trim().ToUpperInvariant();
        if (await uow.Repository<FeeType>().Query().AnyAsync(f =>
                f.PricingPolicyId == policy.PricingPolicyId && f.Code == code && f.FeeTypeId != fee.FeeTypeId, ct))
            return Error.Conflict($"Fee code {code} already exists in this policy.");
        fee.Code = code;
        fee.Name = request.Name.Trim();
        fee.Amount = Money.Round(request.Amount);
        fee.IsPercentage = request.IsPercentage;
        return true;
    }

    private static FeeTypeDto ToDto(FeeType f) => new(f.FeeTypeId, f.PricingPolicyId, f.Code, f.Name, f.Amount, f.IsPercentage);

    private IQueryable<PricingPolicy> WithPrices() => uow.Repository<PricingPolicy>().Query().AsNoTracking()
        .Include(p => p.UnitPrices).ThenInclude(u => u.UnitType)
        .Include(p => p.FeeTypes);

    private static PricingPolicyDto ToDto(PricingPolicy policy) => new(
        policy.PricingPolicyId, policy.Version, policy.EffectiveFrom, policy.EffectiveTo,
        policy.DepositMonths, policy.GraceDays, policy.NoticeDays, policy.LateFeePercentPerWeek, policy.EarlyTerminationPercent,
        policy.SuspendAfterDays, policy.ClearOutAfterDays, policy.CancelFreeHours, policy.Status,
        policy.UnitPrices.OrderBy(p => p.UnitType.Code)
            .Select(p => new UnitPriceDto(p.UnitPriceId, p.PricingPolicyId, p.UnitTypeId, p.UnitType.Code, p.UnitType.Name,
                p.BasePricePerMonth, p.Discount3Month, p.Discount6Month, p.Discount12Month)).ToList(),
        policy.FeeTypes.OrderBy(f => f.Code)
            .Select(ToDto).ToList());
}
