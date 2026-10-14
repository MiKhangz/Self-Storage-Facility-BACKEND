namespace StorEase.Application.Interfaces;

public interface IEmailService
{
    /// <summary>Sends one plain-text email. Returns false when delivery failed, so callers can record it.</summary>
    Task<bool> SendAsync(string to, string subject, string body, CancellationToken ct);
}
