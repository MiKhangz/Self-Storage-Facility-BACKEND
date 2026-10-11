using StorEase.Application.Interfaces;

namespace StorEase.Application.Tests;

public sealed class FakeCurrentUser(int userId, string role) : ICurrentUser
{
    public int UserId { get; } = userId;
    public string Role { get; } = role;
}
