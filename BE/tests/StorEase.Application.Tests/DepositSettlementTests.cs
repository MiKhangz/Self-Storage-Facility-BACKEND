using FluentAssertions;
using StorEase.Application.Features.Returns;

namespace StorEase.Application.Tests;

public class DepositSettlementTests
{
    [Fact]
    public void Refund_is_deposit_minus_damage_minus_outstanding()
    {
        var (refund, shortfall) = DepositSettlement.Calculate(900_000m, 200_000m, 100_000m);
        refund.Should().Be(600_000m);
        shortfall.Should().Be(0m);
    }

    [Fact]
    public void Deductions_above_deposit_give_zero_refund_and_a_shortfall()
    {
        var (refund, shortfall) = DepositSettlement.Calculate(900_000m, 800_000m, 300_000m);
        refund.Should().Be(0m);
        shortfall.Should().Be(200_000m);
    }

    [Fact]
    public void Clean_return_refunds_the_whole_deposit()
    {
        DepositSettlement.Calculate(900_000m, 0m, 0m).Refund.Should().Be(900_000m);
    }

    [Theory]
    [InlineData("2026-12-31", 0)]       // on the end date: no penalty
    [InlineData("2027-01-05", 0)]       // after it: no penalty
    [InlineData("2026-12-30", 200_000)] // one day early: 20% of the 1,000,000 deposit
    public void Early_termination_penalty_applies_only_before_the_end_date(string returnDate, int expected)
    {
        DepositSettlement.EarlyTerminationPenalty(1_000_000m, 20m, DateOnly.Parse(returnDate), new DateOnly(2026, 12, 31))
            .Should().Be(expected);
    }
}
