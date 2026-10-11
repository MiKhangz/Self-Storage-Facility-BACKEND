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

    [Fact]
    public void New_end_date_adds_the_months_to_the_current_end()
    {
        RenewalPricing.NewEndDate(new DateOnly(2027, 1, 31), 1).Should().Be(new DateOnly(2027, 2, 28));
    }
}
