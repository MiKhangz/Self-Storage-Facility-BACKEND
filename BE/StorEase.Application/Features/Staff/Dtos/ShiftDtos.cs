using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Staff.Dtos;

/// <summary>
/// Times are the facility's local wall-clock time. <c>EndTime</c> at or before <c>StartTime</c>
/// means the shift runs overnight and ends the next day.
/// </summary>
public sealed record CreateShiftRequest(
    int UserId,
    int FacilityId,
    DateOnly ShiftDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    ShiftType ShiftType);

/// <summary>Moves a shift in time. The person and the facility cannot change; delete and re-create instead.</summary>
public sealed record UpdateShiftRequest(
    DateOnly ShiftDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    ShiftType ShiftType);

public sealed record ShiftDto(
    int ShiftId,
    int UserId,
    string UserName,
    int FacilityId,
    string FacilityName,
    DateOnly ShiftDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool EndsNextDay,
    ShiftType ShiftType);
