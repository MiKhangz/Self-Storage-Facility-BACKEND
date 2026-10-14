using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Contracts.Dtos;

/// <summary>A contract stays <c>Draft</c> after the customer signs; only <see cref="SignedAt"/> changes.</summary>
public sealed record ContractDto(
    int ContractId,
    string ContractNumber,
    int ReservationId,
    int CustomerId,
    string CustomerName,
    int StorageUnitId,
    string UnitCode,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal MonthlyRent,
    decimal DepositAmount,
    int PaymentDay,
    DateTime? SignedAt,
    ContractStatus Status);

/// <summary><see cref="PaymentDay"/> is the day of the month rent falls due (1–28, so it exists in every month).</summary>
public sealed record CreateContractRequest(int ReservationId, int PaymentDay);
