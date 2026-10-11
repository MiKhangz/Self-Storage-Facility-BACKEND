using StorEase.Domain.Enums;

namespace StorEase.Application.Features.SupportTickets.Dtos;

public sealed record TicketDto(
    int TicketId,
    string TicketCode,
    int CustomerId,
    string CustomerName,
    int FacilityId,
    string FacilityName,
    int? StorageUnitId,
    string? UnitCode,
    int CategoryId,
    string CategoryName,
    string Subject,
    Priority Priority,
    SupportTicketStatus Status,
    int? AssignedTo,
    DateTime? DueDate,
    DateTime CreatedAt,
    DateTime? ResolvedAt);

public sealed record TicketMessageDto(
    int MessageId,
    int TicketId,
    int SenderId,
    string SenderName,
    string Body,
    bool IsInternalNote,
    DateTime SentAt);

public sealed record TicketCategoryDto(int CategoryId, string Name, Priority DefaultPriority);
