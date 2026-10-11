namespace StorEase.Application.Features.Renewals;

public static class RenewalPricing
{
    /// <summary>Base price less the discount (a percentage) of the longest tier the extension reaches.</summary>
    public static decimal Rent(decimal basePricePerMonth, int months, decimal discount3, decimal discount6, decimal discount12)
    {
        var percent = months >= 12 ? discount12 : months >= 6 ? discount6 : months >= 3 ? discount3 : 0m;
        return Math.Round(basePricePerMonth * (100m - percent) / 100m, 2);
    }

    public static DateOnly NewEndDate(DateOnly currentEnd, int months) => currentEnd.AddMonths(months);
}
