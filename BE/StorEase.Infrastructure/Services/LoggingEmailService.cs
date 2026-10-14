using Microsoft.Extensions.Logging;
using StorEase.Application.Interfaces;

namespace StorEase.Infrastructure.Services;

// ponytail: no SMTP provider yet, so emails are written to the API log (links included); swap for an SMTP/SendGrid implementation before real users.
public class LoggingEmailService(ILogger<LoggingEmailService> logger) : IEmailService
{
    public Task<bool> SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        logger.LogInformation("EMAIL to {To} | {Subject}\n{Body}", to, subject, body);
        return Task.FromResult(true);
    }
}
