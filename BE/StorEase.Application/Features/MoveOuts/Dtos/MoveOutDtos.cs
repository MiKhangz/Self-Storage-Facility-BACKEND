using StorEase.Domain.Enums;

namespace StorEase.Application.Features.MoveOuts.Dtos;

public sealed record CreateMoveOutRequest(
    int ContractId, DateOnly PlannedMoveOutDate, string? Reason, RefundMethod? RefundMethod, string? RefundAccount);

public sealed record ApproveMoveOutRequest(DateTime InspectionSlot);

public sealed record CreateReturnInspectionRequest(
    ConditionResult ConditionResult, decimal CleaningFee, decimal RepairCharge, decimal OtherDeduction);

public sealed record ReturnInspectionDto(
    int ReturnInspectionId, DateTime InspectedAt, ConditionResult ConditionResult,
    decimal CleaningFee, decimal RepairCharge, decimal OtherDeduction, decimal RefundAmount,
    ReturnInspectionStatus Status);

public sealed record MoveOutRequestDto(
    int MoveOutRequestId, int ContractId, string ContractNumber, int CustomerId, string CustomerName,
    int StorageUnitId, string UnitCode, DateTime RequestedAt, DateOnly PlannedMoveOutDate, string? Reason,
    DateTime? InspectionSlot, RefundMethod? RefundMethod, string? RefundAccount, MoveOutRequestStatus Status,
    ReturnInspectionDto? Inspection);

/// <summary>Breakdown of the deposit settlement; <c>Shortfall</c> is what the customer still owes when deductions exceed the deposit.</summary>
public sealed record DepositSettlementDto(
    int MoveOutRequestId, decimal DepositAmount, decimal DamageDeductions, decimal OutstandingInvoices,
    decimal RefundAmount, decimal Shortfall, MoveOutRequestStatus Status);
