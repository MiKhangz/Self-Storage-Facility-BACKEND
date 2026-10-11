using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Staff.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Staff;

public interface IStaffTaskService
{
    Task<Result<StaffTaskDto>> CreateAsync(CreateStaffTaskRequest request, CancellationToken ct);
    Task<Result<StaffTaskDto>> GetAsync(int taskId, CancellationToken ct);
    Task<Result<PagedResult<StaffTaskDto>>> ListAsync(
        int? shiftId, int? assignedTo, StaffTaskStatus? status, int page, int pageSize, CancellationToken ct);
    Task<Result<StaffTaskDto>> ChangeStatusAsync(int taskId, ChangeStaffTaskStatusRequest request, CancellationToken ct);
}

public class StaffTaskService(IUnitOfWork uow, ICurrentUser currentUser) : IStaffTaskService
{
    // Shift times are the facility's local wall clock; every facility is in Vietnam (UTC+7, no DST).
    private static readonly TimeSpan LocalOffset = TimeSpan.FromHours(7);

    public async Task<Result<StaffTaskDto>> CreateAsync(CreateStaffTaskRequest request, CancellationToken ct)
    {
        var scope = Scope();
        var shift = await uow.Repository<Shift>().Query()
            .Where(s => s.ShiftId == request.ShiftId && (scope == null || scope.Contains(s.FacilityId)))
            .Select(s => new { s.UserId, s.ShiftDate, s.StartTime, s.EndTime })
            .FirstOrDefaultAsync(ct);
        if (shift is null)
            return Error.NotFound("Shift not found.");

        if (request.AssignedTo != shift.UserId)
            return Error.Validation("The task must be assigned to the person on this shift.");

        var assigneeActive = await uow.Repository<User>().Query()
            .AnyAsync(u => u.UserId == request.AssignedTo && u.Status == UserStatus.Active, ct);
        if (!assigneeActive)
            return Error.Validation("The assignee must be an active account.");

        var (from, to) = ShiftWindowUtc(shift.ShiftDate, shift.StartTime, shift.EndTime);
        var at = VietnamTime.ToUtc(request.ScheduledAt);
        if (at < from || at >= to)
            return Error.Validation("scheduledAt must fall inside the shift.");

        var task = new StaffTask
        {
            ShiftId = request.ShiftId,
            AssignedTo = request.AssignedTo,
            TaskType = request.TaskType,
            ReferenceCode = request.ReferenceCode?.Trim(),
            Note = request.Note?.Trim(),
            ScheduledAt = at,
            Status = StaffTaskStatus.ToDo,
        };
        uow.Repository<StaffTask>().Add(task);
        await uow.SaveChangesAsync(ct);

        return await GetAsync(task.TaskId, ct);
    }

    public async Task<Result<StaffTaskDto>> GetAsync(int taskId, CancellationToken ct)
    {
        var dto = await Project(Visible().Where(t => t.TaskId == taskId)).FirstOrDefaultAsync(ct);
        // A task outside the caller's view is a 404, so ids cannot be probed.
        return dto is null ? Error.NotFound("Task not found.") : dto;
    }

    public async Task<Result<PagedResult<StaffTaskDto>>> ListAsync(
        int? shiftId, int? assignedTo, StaffTaskStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var query = Visible();
        if (shiftId is not null)
            query = query.Where(t => t.ShiftId == shiftId);
        if (assignedTo is not null)
            query = query.Where(t => t.AssignedTo == assignedTo);
        if (status is not null)
            query = query.Where(t => t.Status == status);

        return await Project(query.OrderBy(t => t.ScheduledAt).ThenBy(t => t.TaskId))
            .ToPagedResultAsync(page, pageSize, ct);
    }

    public async Task<Result<StaffTaskDto>> ChangeStatusAsync(
        int taskId, ChangeStaffTaskStatusRequest request, CancellationToken ct)
    {
        var task = await Visible().FirstOrDefaultAsync(t => t.TaskId == taskId, ct);
        if (task is null)
            return Error.NotFound("Task not found.");

        var target = request.Status;
        if (target == StaffTaskStatus.Cancelled)
        {
            if (currentUser.Role != Roles.FacilityManager)
                return Error.Forbidden("Only a facility manager can cancel a task.");
            if (task.Status is StaffTaskStatus.Done or StaffTaskStatus.Cancelled)
                return Error.Conflict($"A {task.Status} task cannot be cancelled.");
        }
        else
        {
            // Progress is reported by whoever the task was given to, not by the manager.
            if (task.AssignedTo != currentUser.UserId)
                return Error.Forbidden("Only the assignee can update the progress of a task.");
            var allowed = (task.Status, target) is
                (StaffTaskStatus.ToDo, StaffTaskStatus.InProgress) or (StaffTaskStatus.InProgress, StaffTaskStatus.Done);
            if (!allowed)
                return Error.Conflict($"A {task.Status} task cannot move to {target}.");
        }

        task.Status = target;
        await uow.SaveChangesAsync(ct);
        return await GetAsync(taskId, ct);
    }

    /// <summary>Null means every facility; otherwise a subquery of the caller's facility ids.</summary>
    private IQueryable<int>? Scope() =>
        StaffScope.SeesAllFacilities(currentUser.Role) ? null : StaffScope.FacilityIdsOf(uow, currentUser.UserId);

    /// <summary>
    /// Admins see all tasks, a manager the tasks of shifts at their facilities, a staff member only their own.
    /// </summary>
    private IQueryable<StaffTask> Visible()
    {
        var query = uow.Repository<StaffTask>().Query();
        if (StaffScope.SeesAllFacilities(currentUser.Role))
            return query;
        var scope = StaffScope.FacilityIdsOf(uow, currentUser.UserId);
        return currentUser.Role == Roles.FacilityManager
            ? query.Where(t => t.AssignedTo == currentUser.UserId ||
                               (t.Shift != null && scope.Contains(t.Shift.FacilityId)))
            : query.Where(t => t.AssignedTo == currentUser.UserId);
    }

    /// <summary>[start, end) in UTC; an end at or before the start means the shift runs past midnight.</summary>
    private static (DateTime From, DateTime To) ShiftWindowUtc(DateOnly date, TimeOnly start, TimeOnly end)
    {
        var from = date.ToDateTime(start);
        var to = date.ToDateTime(end);
        if (end <= start)
            to = to.AddDays(1);
        return (DateTime.SpecifyKind(from - LocalOffset, DateTimeKind.Utc),
                DateTime.SpecifyKind(to - LocalOffset, DateTimeKind.Utc));
    }

    private static IQueryable<StaffTaskDto> Project(IQueryable<StaffTask> query) =>
        query.Select(t => new StaffTaskDto(
            t.TaskId, t.ShiftId, t.Shift == null ? null : t.Shift.FacilityId, t.AssignedTo,
            t.AssignedToUser.FullName, t.TaskType, t.ReferenceCode, t.Note, t.ScheduledAt, t.Status));
}
