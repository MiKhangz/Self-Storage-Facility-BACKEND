namespace StorEase.Application.Features.Renewals.Dtos;

public sealed record CreateRenewalRequest(int ContractId, int ExtendMonths);

/// <summary><c>ContractId</c> links to the contract being extended; its end date moves to <c>NewEndDate</c> once the renewal is approved.</summary>
public sealed record RenewalDto(
    int RenewalId, int ContractId, string ContractNumber, string UnitCode, DateTime RequestedAt,
    int ExtendMonths, DateOnly CurrentEndDate, DateOnly NewEndDate, decimal CurrentMonthlyRent,
    decimal NewMonthlyRent, string Status, int? ApprovedBy);
