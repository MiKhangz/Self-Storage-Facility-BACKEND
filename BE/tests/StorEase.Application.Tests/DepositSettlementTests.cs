using FluentAssertions;
using StorEase.Application.Features.MoveOuts;

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

    [Fact]
    public void Earliest_move_out_date_is_today_plus_notice_days()
    {
        DepositSettlement.EarliestMoveOutDate(new DateOnly(2026, 10, 12), 30).Should().Be(new DateOnly(2026, 11, 11));
    }
}
