namespace StorEase.Application.Features.Facilities.Dtos;

/// <summary><c>Staffed</c> is false when no manager or staff works there: customers do not see it and cannot book it.</summary>
public sealed record FacilityListItemDto(
    int FacilityId,
    string Code,
    string Name,
    string Address,
    string District,
    string? Phone,
    string? OpeningHours,
    int AvailableUnits,
    bool Staffed);
