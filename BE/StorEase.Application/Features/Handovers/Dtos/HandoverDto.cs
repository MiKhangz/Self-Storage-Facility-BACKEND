using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Handovers.Dtos;

/// <summary><see cref="AccessCode"/> is plaintext and returned only here, once.</summary>
public sealed record HandoverDto(
    int HandoverId,
    int ReservationId,
    int ContractId,
    int StorageUnitId,
    string UnitCode,
    DateTime HandoverTime,
    string LockSerial,
    string? CardNumber,
    string? ConditionNote,
    HandoverStatus Status,
    string AccessCode);
