using StorEase.Domain.Enums;

namespace StorEase.Application.Features.SupportTickets.Dtos;

/// <summary>
/// <c>IsInternalNote</c> and <c>NextStatus</c> are staff-only. <c>NextStatus</c> may be
/// InProgress or WaitingForCustomer; left empty, a first reply moves a New ticket to InProgress.
/// </summary>
public sealed record PostTicketMessageRequest(string Body, bool IsInternalNote, SupportTicketStatus? NextStatus);
