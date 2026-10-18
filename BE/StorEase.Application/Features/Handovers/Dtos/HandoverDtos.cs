using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Handovers.Dtos;

public sealed record CreateHandoverRequest(
    int ContractId, DateOnly PlannedHandoverDate, string? Reason, RefundMethod? RefundMethod, string? RefundAccount);

public sealed record ApproveHandoverRequest(DateTime InspectionSlot);

public sealed record CreateHandoverInspectionRequest(
    ConditionResult ConditionResult, decimal CleaningFee, decimal RepairCharge, decimal OtherDeduction);

public sealed record HandoverInspectionDto(
    int HandoverInspectionId, DateTime InspectedAt, ConditionResult ConditionResult,
    decimal CleaningFee, decimal RepairCharge, decimal OtherDeduction, decimal RefundAmount,
    HandoverInspectionStatus Status);

public sealed record HandoverDto(
    int HandoverId, int ContractId, string ContractNumber, int CustomerId, string CustomerName,
    int StorageUnitId, string UnitCode, DateTime RequestedAt, DateOnly PlannedHandoverDate, string? Reason,
    DateTime? InspectionSlot, RefundMethod? RefundMethod, string? RefundAccount, HandoverStatus Status,
    HandoverInspectionDto? Inspection);

/// <summary>Breakdown of the deposit settlement; <c>Shortfall</c> is what the customer still owes when deductions exceed the deposit.</summary>
/// <summary>EarlyTerminationPenalty is also added to the inspection's OtherDeduction; DamageDeductions excludes it.</summary>
public sealed record DepositSettlementDto(
    int HandoverId, decimal DepositAmount, decimal DamageDeductions, decimal EarlyTerminationPenalty, decimal OutstandingInvoices,
    decimal RefundAmount, decimal Shortfall, HandoverStatus Status);
