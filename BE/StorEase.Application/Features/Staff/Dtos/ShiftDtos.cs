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

/// <summary>One cell of the weekly timetable: a day and its morning (07–15) or afternoon (15–23) shift.</summary>
public sealed record TimetableSlot(DayOfWeek Day, ShiftType ShiftType);

/// <summary>The staff member's usual week at one facility. Saving rebuilds their shifts for the next two weeks.</summary>
public sealed record SaveTimetableRequest(int UserId, int FacilityId, IReadOnlyList<TimetableSlot>? Slots);

/// <param name="Through">Shifts have been created up to this day.</param>
/// <param name="Skipped">Slots not turned into a shift on save, with the reason (e.g. a clash).</param>
public sealed record TimetableDto(
    int UserId, int FacilityId, IReadOnlyList<TimetableSlot> Slots, DateOnly? Through, IReadOnlyList<string> Skipped);
