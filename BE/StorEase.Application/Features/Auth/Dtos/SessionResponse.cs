namespace StorEase.Application.Features.Auth.Dtos;

/// <summary>Who is signed in and when the session ends (the login token's expiry).</summary>
public sealed record SessionResponse(int UserId, string FullName, string Role, DateTime ExpiresAt);
