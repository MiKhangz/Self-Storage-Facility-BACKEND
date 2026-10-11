namespace StorEase.Application.Features.AccessCodes.Dtos;

/// <summary>The plaintext <see cref="Code"/> is shown once, at issue time; only its hash is stored.</summary>
public sealed record AccessCodeDto(int AccessCodeId, int ContractId, string Code, DateTime IssuedAt, DateTime ExpiresAt);
