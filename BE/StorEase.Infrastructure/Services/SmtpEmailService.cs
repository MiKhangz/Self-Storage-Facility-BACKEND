using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using StorEase.Application.Interfaces;

namespace StorEase.Infrastructure.Services;

/// <summary>The <c>Smtp</c> section. Leave <see cref="Host"/> empty to keep emails in the API log instead.</summary>
public sealed class SmtpOptions
{
    public string? Host { get; init; }
    public int Port { get; init; } = 587;
    public bool EnableSsl { get; init; } = true;
    public string? UserName { get; init; }
    public string? Password { get; init; }
    /// <summary>Sender address; defaults to <see cref="UserName"/>.</summary>
    public string? From { get; init; }
}

/// <summary>Sends plain-text email through an SMTP server (Gmail app password, Mailtrap, SendGrid SMTP, ...).</summary>
public class SmtpEmailService(SmtpOptions options, ILogger<SmtpEmailService> logger) : IEmailService
{
    public async Task<bool> SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        try
        {
            using var client = new SmtpClient(options.Host, options.Port) { EnableSsl = options.EnableSsl };
            if (!string.IsNullOrEmpty(options.UserName))
                client.Credentials = new NetworkCredential(options.UserName, options.Password);
            using var message = new MailMessage(options.From ?? options.UserName!, to, subject, body);
            await client.SendMailAsync(message, ct);
            return true;
        }
        // Callers record a failed delivery (see IEmailService); one bad address must not fail the request.
        catch (Exception ex) when (ex is SmtpException or FormatException or InvalidOperationException or ArgumentException)
        {
            logger.LogWarning(ex, "Email to {To} ({Subject}) was not sent", to, subject);
            return false;
        }
    }
}
