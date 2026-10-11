using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Staff.Dtos;

public sealed record StaffAccountDto(
    int UserId,
    string FullName,
    string Email,
    string PhoneNumber,
    string Role,
    UserStatus Status,
    DateTime CreatedAt,
    IReadOnlyList<StaffFacilityDto> Facilities);

public sealed record StaffFacilityDto(int FacilityId, string FacilityName);

public sealed record CreateStaffAccountRequest(
    string FullName, string Email, string PhoneNumber, string Password, string Role);

/// <summary>Every field is optional; only the ones given are changed.</summary>
public sealed record UpdateStaffAccountRequest(
    string? FullName, string? PhoneNumber, string? Role, UserStatus? Status);

public sealed record SetStaffFacilitiesRequest(IReadOnlyList<int> FacilityIds);
