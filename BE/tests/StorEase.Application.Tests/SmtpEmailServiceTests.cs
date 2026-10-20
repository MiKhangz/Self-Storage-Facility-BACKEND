using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StorEase.Infrastructure.Services;

namespace StorEase.Application.Tests;

public class SmtpEmailServiceTests
{
    [Fact]
    public async Task An_unreachable_server_reports_failure_instead_of_throwing()
    {
        var service = new SmtpEmailService(
            new SmtpOptions { Host = "127.0.0.1", Port = 1, EnableSsl = false, From = "noreply@storease.vn" },
            NullLogger<SmtpEmailService>.Instance);

        var sent = await service.SendAsync("customer@x.vn", "Hi", "Body", default);

        sent.Should().BeFalse();
    }
}
