namespace StorEase.Application.Features.Auth.Dtos;

public sealed record LoginResponse(string Token, int UserId, string FullName, string Role);
