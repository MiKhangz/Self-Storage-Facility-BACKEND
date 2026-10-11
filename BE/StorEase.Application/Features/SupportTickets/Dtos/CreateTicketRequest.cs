using StorEase.Domain.Enums;

namespace StorEase.Application.Features.SupportTickets.Dtos;

/// <summary>Give <c>StorageUnitId</c> (the facility is taken from the unit) or <c>FacilityId</c>. Priority defaults to the category's.</summary>
public sealed record CreateTicketRequest(
    int CategoryId,
    string Subject,
    string Description,
    int? StorageUnitId,
    int? FacilityId,
    Priority? Priority);
