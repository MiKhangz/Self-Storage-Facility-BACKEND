using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Returns.Dtos;

public sealed record CreateReturnRequest(
    int ContractId, DateOnly PlannedReturnDate, string? Reason, RefundMethod? RefundMethod, string? RefundAccount);

public sealed record ApproveReturnRequest(DateTime InspectionSlot);

public sealed record CreateReturnInspectionRequest(
    ConditionResult ConditionResult, decimal CleaningFee, decimal RepairCharge, decimal OtherDeduction);

public sealed record ReturnInspectionDto(
    int ReturnInspectionId, DateTime InspectedAt, ConditionResult ConditionResult,
    decimal CleaningFee, decimal RepairCharge, decimal OtherDeduction, decimal RefundAmount,
    ReturnInspectionStatus Status);

public sealed record ReturnDto(
    int UnitReturnId, int ContractId, string ContractNumber, int CustomerId, string CustomerName,
    int StorageUnitId, string UnitCode, DateTime RequestedAt, DateOnly PlannedReturnDate, string? Reason,
    DateTime? InspectionSlot, RefundMethod? RefundMethod, string? RefundAccount, ReturnStatus Status,
    ReturnInspectionDto? Inspection);

/// <summary>Breakdown of the deposit settlement; <c>Shortfall</c> is what the customer still owes when deductions exceed the deposit.</summary>
/// <summary>EarlyTerminationPenalty is also added to the inspection's OtherDeduction; DamageDeductions excludes it.</summary>
public sealed record DepositSettlementDto(
    int UnitReturnId, decimal DepositAmount, decimal DamageDeductions, decimal EarlyTerminationPenalty, decimal OutstandingInvoices,
    decimal RefundAmount, decimal Shortfall, ReturnStatus Status);
