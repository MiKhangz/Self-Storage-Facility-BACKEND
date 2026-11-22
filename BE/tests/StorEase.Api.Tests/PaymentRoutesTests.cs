using FluentAssertions;
using StorEase.Api.Controllers;

namespace StorEase.Api.Tests;

public class PaymentRoutesTests
{
    [Fact]
    public void Money_is_confirmed_only_by_VNPay_or_the_test_checkout() =>
        typeof(PaymentsController).GetMethod("GatewayCallback").Should().BeNull();
}
