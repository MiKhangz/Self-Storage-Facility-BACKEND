using StorEase.Application.Interfaces;

namespace StorEase.Application.Tests;

public sealed class FakeEmailService(bool delivers = true) : IEmailService
{
    public List<(string To, string Subject, string Body)> Sent { get; } = [];

    public Task<bool> SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        Sent.Add((to, subject, body));
        return Task.FromResult(delivers);
    }
}
