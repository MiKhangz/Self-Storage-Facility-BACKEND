using StorEase.Domain.Enums;

namespace StorEase.Application.Features.CheckIns.Dtos;

/// <summary><see cref="AccessCode"/> is plaintext and returned only here, once.</summary>
public sealed record CheckInDto(
    int CheckInId,
    int ReservationId,
    int ContractId,
    int StorageUnitId,
    string UnitCode,
    DateTime CheckInTime,
    string LockSerial,
    string CardNumber,
    string? ConditionNote,
    CheckInStatus Status,
    string AccessCode);
