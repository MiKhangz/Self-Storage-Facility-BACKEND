using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Pricing;

/// <summary><paramref name="FacilityPriceVersionId"/> is null when the company standard price applied.</summary>
public sealed record RentTerms(PricingPolicy Policy, decimal MonthlyRent, decimal Deposit, int? FacilityPriceVersionId);

/// <summary>A size's monthly price and long-stay discounts, from a facility's own list or the company standard.</summary>
public sealed record SizePrice(decimal BasePricePerMonth, decimal Discount3Month, decimal Discount6Month, decimal Discount12Month, int? FacilityPriceVersionId)
{
    public decimal Discount(int months) => months >= 12 ? Discount12Month : months >= 6 ? Discount6Month : months >= 3 ? Discount3Month : 0m;
}

/// <summary>What a booking was quoted under, so its contract gets the same rent and rules.</summary>
public sealed record PricePin(int? PricingPolicyId, int? FacilityPriceVersionId);

/// <summary>
/// The one place the rent for a stay is worked out, so the quote, the deposit invoice and the contract can never
/// disagree. Business rule 4: the rent is fixed from the prices in force on the move-in date. Rules (deposit months,
/// grace, late fee…) always come from the company standard policy; the monthly price and discounts of a size come
/// from the facility's own price list where it has one, else from the standard.
/// </summary>
public static class RentCalculator
{
    // A Superseded version still governs the dates it covered; only Drafts were never in force.
    public static Task<PricingPolicy?> PolicyForAsync(IUnitOfWork uow, DateOnly date, CancellationToken ct) =>
        uow.Repository<PricingPolicy>().Query()
            .Where(p => (p.Status == PricingPolicyStatus.Active || p.Status == PricingPolicyStatus.Superseded) && p.EffectiveFrom <= date
                && (p.EffectiveTo == null || p.EffectiveTo >= date))
            .OrderByDescending(p => p.EffectiveFrom)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// A rental's own policy version (pinned when it was booked), or for rentals from before pinning, the one in force
    /// on <paramref name="date"/>.
    /// </summary>
    public static async Task<PricingPolicy?> PolicyOfAsync(IUnitOfWork uow, int? pricingPolicyId, DateOnly date, CancellationToken ct) =>
        pricingPolicyId is { } id
            ? await uow.Repository<PricingPolicy>().FindAsync(id, ct)
            : await PolicyForAsync(uow, date, ct);

    /// <summary>The facility's own price list version in force on a date; null when it has none (the standard applies).</summary>
    public static Task<int?> FacilityVersionForAsync(IUnitOfWork uow, int facilityId, DateOnly date, CancellationToken ct) =>
        InForce(uow, date).Where(v => v.FacilityId == facilityId)
            .OrderByDescending(v => v.EffectiveFrom).ThenByDescending(v => v.Version)
            .Select(v => (int?)v.FacilityPriceVersionId).FirstOrDefaultAsync(ct);

    private static IQueryable<FacilityPriceVersion> InForce(IUnitOfWork uow, DateOnly date) =>
        uow.Repository<FacilityPriceVersion>().Query()
            .Where(v => (v.Status == PricingPolicyStatus.Active || v.Status == PricingPolicyStatus.Superseded)
                && v.EffectiveFrom <= date && (v.EffectiveTo == null || v.EffectiveTo >= date));

    /// <summary>
    /// A size's price: the facility version's own price when it lists the size, else the standard policy's.
    /// Pass the facility version id itself (from <see cref="FacilityVersionForAsync"/> or a pin), null for the standard.
    /// </summary>
    public static async Task<SizePrice?> PriceAsync(IUnitOfWork uow, int pricingPolicyId, int unitTypeId, int? facilityPriceVersionId, CancellationToken ct)
    {
        if (facilityPriceVersionId is { } versionId)
        {
            var own = await uow.Repository<FacilityUnitPrice>().Query()
                .Where(p => p.FacilityPriceVersionId == versionId && p.UnitTypeId == unitTypeId)
                .Select(p => new SizePrice(p.BasePricePerMonth, p.Discount3Month, p.Discount6Month, p.Discount12Month, versionId))
                .FirstOrDefaultAsync(ct);
            if (own is not null)
                return own;
        }
        return await uow.Repository<UnitPrice>().Query()
            .Where(p => p.PricingPolicyId == pricingPolicyId && p.UnitTypeId == unitTypeId)
            .Select(p => new SizePrice(p.BasePricePerMonth, p.Discount3Month, p.Discount6Month, p.Discount12Month, null))
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>A size's price at a facility on a date: its own list in force then, else the standard.</summary>
    public static async Task<SizePrice?> PriceAtAsync(IUnitOfWork uow, int pricingPolicyId, int facilityId, int unitTypeId, DateOnly date, CancellationToken ct) =>
        await PriceAsync(uow, pricingPolicyId, unitTypeId, await FacilityVersionForAsync(uow, facilityId, date, ct), ct);

    /// <summary>
    /// Base monthly price per size on a date. At a facility its own list wins; with no facility, each size's lowest
    /// price anywhere, for "from" prices.
    /// </summary>
    public static async Task<Dictionary<int, decimal>> PricesAsync(IUnitOfWork uow, int pricingPolicyId, int? facilityId, DateOnly date, CancellationToken ct)
    {
        var standard = await uow.Repository<UnitPrice>().Query().Where(p => p.PricingPolicyId == pricingPolicyId)
            .ToDictionaryAsync(p => p.UnitTypeId, p => p.BasePricePerMonth, ct);
        var versions = InForce(uow, date);
        if (facilityId is { } id)
        {
            var versionId = await FacilityVersionForAsync(uow, id, date, ct);
            versions = versions.Where(v => v.FacilityPriceVersionId == versionId);
        }
        var own = await uow.Repository<FacilityUnitPrice>().Query()
            .Where(p => versions.Any(v => v.FacilityPriceVersionId == p.FacilityPriceVersionId))
            .Select(p => new { p.UnitTypeId, p.BasePricePerMonth }).ToListAsync(ct);
        foreach (var price in own)
            standard[price.UnitTypeId] = facilityId is null && standard.TryGetValue(price.UnitTypeId, out var current)
                ? Math.Min(current, price.BasePricePerMonth) : price.BasePricePerMonth;
        return standard;
    }

    /// <summary>
    /// Rent and deposit for a stay at a facility. A new booking passes no pin and is quoted from what is in force on the
    /// move-in date; a booking being turned into a contract passes the pin it was quoted under, so a price change in
    /// between does not reach it.
    /// </summary>
    public static async Task<Result<RentTerms>> TermsAsync(
        IUnitOfWork uow, int facilityId, int unitTypeId, DateOnly moveInDate, int rentalMonths, CancellationToken ct, PricePin? pin = null)
    {
        var policy = await PolicyOfAsync(uow, pin?.PricingPolicyId, moveInDate, ct);
        if (policy is null)
            return Error.NotFound("No active pricing policy applies to the move-in date.");
        var versionId = pin is null ? await FacilityVersionForAsync(uow, facilityId, moveInDate, ct) : pin.FacilityPriceVersionId;

        var price = await PriceAsync(uow, policy.PricingPolicyId, unitTypeId, versionId, ct);
        if (price is null)
            return Error.NotFound("No price is configured for this unit type in the active policy.");

        var monthlyRent = Money.Round(price.BasePricePerMonth * (1m - price.Discount(rentalMonths) / 100m));
        return new RentTerms(policy, monthlyRent, Money.Round(monthlyRent * policy.DepositMonths), price.FacilityPriceVersionId);
    }
}
