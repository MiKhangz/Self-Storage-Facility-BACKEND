using StorEase.Application.Features.StorageUnits;

namespace StorEase.Application.Features.Renewals;

public static class RenewalPricing
{
    /// <summary>Base price less the discount (a percentage) of the longest tier the extension reaches.</summary>
    public static decimal Rent(decimal basePricePerMonth, int months, decimal discount3, decimal discount6, decimal discount12)
    {
        var percent = months >= 12 ? discount12 : months >= 6 ? discount6 : months >= 3 ? discount3 : 0m;
        return Math.Round(basePricePerMonth * (100m - percent) / 100m, 2);
    }

    /// <summary>
    /// Counted from the start date, as billing does: adding months to the end date loses days at every short month.
    /// </summary>
    public static DateOnly NewEndDate(DateOnly start, DateOnly currentEnd, int months)
    {
        var billed = 0;
        while (start.AddMonths(billed) <= currentEnd)
            billed++;
        return UnitSchedule.EndOf(start, billed + months);
    }
}
