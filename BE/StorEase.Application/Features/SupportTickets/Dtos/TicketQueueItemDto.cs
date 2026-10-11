using StorEase.Domain.Enums;

namespace StorEase.Application.Features.SupportTickets.Dtos;

/// <summary>One row of the ticket queue; also returned by assign and change-status.</summary>
public sealed record TicketQueueItemDto(
    int TicketId,
    string TicketCode,
    string Subject,
    Priority Priority,
    SupportTicketStatus Status,
    int CategoryId,
    string CategoryName,
    int CustomerId,
    string CustomerName,
    int FacilityId,
    string FacilityName,
    int? StorageUnitId,
    string? UnitCode,
    int? AssignedTo,
    string? AssignedToName,
    DateTime? DueDate,
    DateTime CreatedAt,
    DateTime? ResolvedAt);
