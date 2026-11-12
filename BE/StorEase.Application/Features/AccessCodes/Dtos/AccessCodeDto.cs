namespace StorEase.Application.Features.AccessCodes.Dtos;

/// <summary>The plaintext <see cref="Code"/> is shown once, at issue time; only its hash is stored.</summary>
public sealed record AccessCodeDto(int AccessCodeId, int ContractId, string Code, DateTime IssuedAt, DateTime ExpiresAt);

/// <summary>A code as it can be shown again: no plaintext, which only exists at issue time.</summary>
/// <summary><c>Code</c> is set only for the renter themselves, and only for codes kept encrypted.</summary>
public sealed record AccessCodeSummaryDto(int AccessCodeId, int ContractId, DateTime IssuedAt, DateTime ExpiresAt, bool IsActive, string? Code = null);

public sealed record AccessLogDto(
    long AccessLogId, int AccessCodeId, int ContractId, int CustomerId, string CustomerName,
    int StorageUnitId, string UnitCode, DateTime AccessTime, string Result, string? DeviceId);
