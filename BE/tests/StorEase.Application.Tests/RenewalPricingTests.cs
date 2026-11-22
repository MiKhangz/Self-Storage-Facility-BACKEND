using FluentAssertions;
using StorEase.Application.Features.Renewals;

namespace StorEase.Application.Tests;

public class RenewalPricingTests
{
    // Tiers used below: 3 months 5%, 6 months 10%, 12 months 20%.
    [Theory]
    [InlineData(1, 1_000_000)]
    [InlineData(2, 1_000_000)]
    [InlineData(3, 950_000)]
    [InlineData(5, 950_000)]
    [InlineData(6, 900_000)]
    [InlineData(11, 900_000)]
    [InlineData(12, 800_000)]
    [InlineData(24, 800_000)]
    public void Rent_uses_the_longest_discount_tier_reached(int months, decimal expected)
    {
        RenewalPricing.Rent(1_000_000m, months, 5m, 10m, 20m).Should().Be(expected);
    }

    [Theory]
    [InlineData("2026-12-01", "2027-02-28", 1, "2027-03-31")] // to the month's last day, not the 28th
    [InlineData("2027-01-01", "2027-01-31", 1, "2027-02-28")]
    [InlineData("2027-03-01", "2027-04-30", 6, "2027-10-31")]
    [InlineData("2026-11-30", "2027-02-27", 1, "2027-03-29")] // February does not pull the later months forward
    [InlineData("2026-10-31", "2027-01-30", 1, "2027-02-27")]
    [InlineData("2026-10-31", "2027-02-27", 1, "2027-03-30")]
    [InlineData("2027-12-01", "2028-01-31", 1, "2028-02-29")]
    [InlineData("2026-12-01", "2027-03-28", 1, "2027-04-30")] // renewed under the old rule: the month already billed counts
    [InlineData("2026-01-10", "2026-04-09", 3, "2026-07-09")]
    public void New_end_date_keeps_to_the_rentals_own_billing_months(string start, string end, int months, string expected)
    {
        RenewalPricing.NewEndDate(DateOnly.Parse(start), DateOnly.Parse(end), months).Should().Be(DateOnly.Parse(expected));
    }
}
