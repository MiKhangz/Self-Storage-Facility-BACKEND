using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Pricing;

public sealed record RentTerms(PricingPolicy Policy, decimal MonthlyRent, decimal Deposit);

/// <summary>
/// The one place the rent for a stay is worked out, so the quote, the deposit invoice and the contract can never
/// disagree. Business rule 4: the rent is fixed from the policy in force on the move-in date.
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

    public static async Task<Result<RentTerms>> TermsAsync(
        IUnitOfWork uow, int unitTypeId, DateOnly moveInDate, int rentalMonths, CancellationToken ct)
    {
        var policy = await PolicyForAsync(uow, moveInDate, ct);
        if (policy is null)
            return Error.NotFound("No active pricing policy applies to the move-in date.");

        var price = await uow.Repository<UnitPrice>().Query()
            .FirstOrDefaultAsync(p => p.PricingPolicyId == policy.PricingPolicyId && p.UnitTypeId == unitTypeId, ct);
        if (price is null)
            return Error.NotFound("No price is configured for this unit type in the active policy.");

        var discount = rentalMonths >= 12 ? price.Discount12Month
            : rentalMonths >= 6 ? price.Discount6Month
            : rentalMonths >= 3 ? price.Discount3Month : 0m;
        var monthlyRent = Money.Round(price.BasePricePerMonth * (1m - discount / 100m));
        return new RentTerms(policy, monthlyRent, Money.Round(monthlyRent * policy.DepositMonths));
    }
}
