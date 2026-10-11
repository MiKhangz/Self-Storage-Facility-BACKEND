using StorEase.Domain.Enums;

namespace StorEase.Application.Features.SupportTickets.Dtos;

public sealed record AssignTicketRequest(int AssigneeId, DateTime? DueDate);

public sealed record ChangeTicketStatusRequest(SupportTicketStatus Status);
