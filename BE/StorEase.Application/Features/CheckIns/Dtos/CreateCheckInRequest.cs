namespace StorEase.Application.Features.CheckIns.Dtos;

public sealed record CreateCheckInRequest(
    int ReservationId,
    int ContractId,
    string LockSerial,
    string CardNumber,
    string? ConditionNote);
