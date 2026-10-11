using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Maintenance.Dtos;

/// <summary>Give <c>TicketId</c> (the ticket must name a unit) or <c>StorageUnitId</c>; if both, they must agree.</summary>
public sealed record CreateMaintenanceOrderRequest(
    string Description,
    int? StorageUnitId,
    int? TicketId,
    DateTime? ScheduledAt,
    int? AssignedTo);

/// <summary><c>ScheduledAt</c> is required for Scheduled; <c>Cost</c> and <c>WorkNote</c> for Done.</summary>
public sealed record ChangeMaintenanceStatusRequest(
    MaintenanceOrderStatus Status,
    DateTime? ScheduledAt,
    decimal? Cost,
    string? WorkNote);

public sealed record MaintenanceOrderDto(
    int MaintenanceOrderId,
    int StorageUnitId,
    string UnitCode,
    int FacilityId,
    int? TicketId,
    int? AssignedTo,
    string Description,
    DateTime? ScheduledAt,
    DateTime? CompletedAt,
    decimal? Cost,
    string? WorkNote,
    MaintenanceOrderStatus Status);
