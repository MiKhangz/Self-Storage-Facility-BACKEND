using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Staff.Dtos;

/// <summary><c>ScheduledAt</c> must fall inside the shift; <c>Note</c> is required for <c>Other</c>.</summary>
public sealed record CreateStaffTaskRequest(
    int ShiftId,
    int AssignedTo,
    TaskType TaskType,
    string? ReferenceCode,
    string? Note,
    DateTime ScheduledAt);

public sealed record ChangeStaffTaskStatusRequest(StaffTaskStatus Status);

public sealed record StaffTaskDto(
    int TaskId,
    int? ShiftId,
    int? FacilityId,
    int AssignedTo,
    string AssignedToName,
    TaskType TaskType,
    string? ReferenceCode,
    string? Note,
    DateTime ScheduledAt,
    StaffTaskStatus Status);
