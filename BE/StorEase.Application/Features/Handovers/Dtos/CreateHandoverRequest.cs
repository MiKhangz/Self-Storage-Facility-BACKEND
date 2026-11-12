namespace StorEase.Application.Features.Handovers.Dtos;

public sealed record CreateHandoverRequest(
    int ReservationId,
    int ContractId,
    string LockSerial,
    string? CardNumber,
    string? ConditionNote);
