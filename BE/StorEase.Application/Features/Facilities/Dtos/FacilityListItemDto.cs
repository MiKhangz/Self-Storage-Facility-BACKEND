namespace StorEase.Application.Features.Facilities.Dtos;

public sealed record FacilityListItemDto(
    int FacilityId,
    string Code,
    string Name,
    string Address,
    string District,
    string? Phone,
    string? OpeningHours,
    int AvailableUnits);
