using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Staff.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Staff;

public interface IShiftService
{
    Task<Result<ShiftDto>> CreateAsync(CreateShiftRequest request, CancellationToken ct);
    Task<Result<ShiftDto>> GetAsync(int shiftId, CancellationToken ct);
    Task<Result<PagedResult<ShiftDto>>> ListAsync(
        int? facilityId, int? userId, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct);
    Task<Result<ShiftDto>> UpdateAsync(int shiftId, UpdateShiftRequest request, CancellationToken ct);
    Task<Result<bool>> DeleteAsync(int shiftId, CancellationToken ct);
}

public class ShiftService(IUnitOfWork uow, ICurrentUser currentUser) : IShiftService
{
    // Facilities are all in Vietnam, so "now" for shift rules is a fixed UTC+7 (no DST there).
    private static readonly TimeSpan LocalOffset = TimeSpan.FromHours(7);

    public async Task<Result<ShiftDto>> CreateAsync(CreateShiftRequest request, CancellationToken ct)
    {
        var created = await uow.InTransactionAsync<int>(async () =>
        {
            if (!await CanManageAsync(request.FacilityId, ct))
                return Error.NotFound("Facility not found.");

            var assignee = await uow.Repository<User>().Query()
                .Where(u => u.UserId == request.UserId)
                .Select(u => new { u.Status, Role = u.Role.RoleName })
                .FirstOrDefaultAsync(ct);
            if (assignee is null)
                return Error.NotFound("Staff member not found.");
            if (assignee.Status != UserStatus.Active || assignee.Role is not (Roles.FacilityStaff or Roles.FacilityManager))
                return Error.Conflict("Only an Active facility staff member or manager can be given a shift.");
            if (!await uow.Repository<StaffFacility>().Query()
                    .AnyAsync(s => s.UserId == request.UserId && s.FacilityId == request.FacilityId, ct))
                return Error.Conflict("This person is not assigned to the facility.");

            if (Start(request.ShiftDate, request.StartTime) < LocalNow())
                return Error.Conflict("A shift in the past cannot be created.");

            var clash = await FindOverlapAsync(request.UserId, null, request.ShiftDate, request.StartTime, request.EndTime, ct);
            if (clash is not null)
                return clash;

            // ponytail: check-then-insert, so two managers booking the same person at the same instant could both pass; add a serializable transaction or a lock row if that ever matters.
            var shift = new Shift
            {
                UserId = request.UserId,
                FacilityId = request.FacilityId,
                ShiftDate = request.ShiftDate,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                ShiftType = request.ShiftType,
            };
            uow.Repository<Shift>().Add(shift);
            await uow.SaveChangesAsync(ct);
            return shift.ShiftId;
        }, ct);

        return created.IsSuccess ? await GetAsync(created.Value, ct) : created.Error;
    }

    public async Task<Result<ShiftDto>> GetAsync(int shiftId, CancellationToken ct)
    {
        var dto = await Project(Visible().Where(s => s.ShiftId == shiftId)).FirstOrDefaultAsync(ct);
        return dto is null ? Error.NotFound("Shift not found.") : dto;
    }

    public async Task<Result<PagedResult<ShiftDto>>> ListAsync(
        int? facilityId, int? userId, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct)
    {
        var query = Visible();
        if (facilityId is not null)
            query = query.Where(s => s.FacilityId == facilityId);
        if (userId is not null)
            query = query.Where(s => s.UserId == userId);
        if (from is not null)
            query = query.Where(s => s.ShiftDate >= from);
        if (to is not null)
            query = query.Where(s => s.ShiftDate <= to);

        return await Project(query.OrderBy(s => s.ShiftDate).ThenBy(s => s.StartTime).ThenBy(s => s.ShiftId))
            .ToPagedResultAsync(page, pageSize, ct);
    }

    public async Task<Result<ShiftDto>> UpdateAsync(int shiftId, UpdateShiftRequest request, CancellationToken ct)
    {
        var updated = await uow.InTransactionAsync<int>(async () =>
        {
            var shift = await Manageable().FirstOrDefaultAsync(s => s.ShiftId == shiftId, ct);
            if (shift is null)
                return Error.NotFound("Shift not found.");
            if (End(shift.ShiftDate, shift.StartTime, shift.EndTime) <= LocalNow())
                return Error.Conflict("A shift that has already ended cannot be changed.");
            if (Start(request.ShiftDate, request.StartTime) < LocalNow())
                return Error.Conflict("A shift cannot be moved into the past.");

            var clash = await FindOverlapAsync(shift.UserId, shift.ShiftId, request.ShiftDate, request.StartTime, request.EndTime, ct);
            if (clash is not null)
                return clash;

            shift.ShiftDate = request.ShiftDate;
            shift.StartTime = request.StartTime;
            shift.EndTime = request.EndTime;
            shift.ShiftType = request.ShiftType;
            return shift.ShiftId;
        }, ct);

        return updated.IsSuccess ? await GetAsync(updated.Value, ct) : updated.Error;
    }

    public async Task<Result<bool>> DeleteAsync(int shiftId, CancellationToken ct) =>
        await uow.InTransactionAsync<bool>(async () =>
        {
            var shift = await Manageable().FirstOrDefaultAsync(s => s.ShiftId == shiftId, ct);
            if (shift is null)
                return Error.NotFound("Shift not found.");
            // A shift is a plan, not history: only one that has not begun, and carries no tasks, may go.
            if (Start(shift.ShiftDate, shift.StartTime) <= LocalNow())
                return Error.Conflict("A shift that has started cannot be deleted.");
            if (await uow.Repository<StaffTask>().Query().AnyAsync(t => t.ShiftId == shiftId, ct))
                return Error.Conflict("This shift has tasks; remove or cancel them first.");

            uow.Repository<Shift>().Remove(shift);
            return true;
        }, ct);

    /// <summary>
    /// Two shifts of one person clash when their half-open intervals [start, end) intersect, at any facility.
    /// The day before is loaded too, because an overnight shift of yesterday runs into today.
    /// </summary>
    private async Task<Error?> FindOverlapAsync(
        int userId, int? ignoreShiftId, DateOnly date, TimeOnly startTime, TimeOnly endTime, CancellationToken ct)
    {
        var start = Start(date, startTime);
        var end = End(date, startTime, endTime);
        var from = date.AddDays(-1);
        var to = date.AddDays(1);

        var nearby = await uow.Repository<Shift>().Query().AsNoTracking()
            .Where(s => s.UserId == userId && s.ShiftId != ignoreShiftId && s.ShiftDate >= from && s.ShiftDate <= to)
            .Select(s => new { s.ShiftId, s.ShiftDate, s.StartTime, s.EndTime, Facility = s.Facility.Name })
            .ToListAsync(ct);

        var clash = nearby.FirstOrDefault(s =>
            Start(s.ShiftDate, s.StartTime) < end && start < End(s.ShiftDate, s.StartTime, s.EndTime));
        return clash is null
            ? null
            : Error.Conflict($"Overlaps shift #{clash.ShiftId} at {clash.Facility} ({clash.ShiftDate:yyyy-MM-dd} {clash.StartTime:HH\\:mm}-{clash.EndTime:HH\\:mm}).");
    }

    private static DateTime Start(DateOnly date, TimeOnly start) => date.ToDateTime(start);

    private static DateTime End(DateOnly date, TimeOnly start, TimeOnly end) =>
        end > start ? date.ToDateTime(end) : date.AddDays(1).ToDateTime(end);

    private static DateTime LocalNow() => DateTime.UtcNow + LocalOffset;

    /// <summary>The administrator manages every facility; a facility manager only their own scope.</summary>
    private async Task<bool> CanManageAsync(int facilityId, CancellationToken ct)
    {
        var facilities = uow.Repository<Facility>().Query().Where(f => f.FacilityId == facilityId);
        if (currentUser.Role == Roles.SystemAdministrator)
            return await facilities.AnyAsync(ct);
        if (currentUser.Role != Roles.FacilityManager)
            return false;
        var ids = StaffScope.FacilityIdsOf(uow, currentUser.UserId);
        return await facilities.AnyAsync(f => ids.Contains(f.FacilityId), ct);
    }

    private IQueryable<Shift> Manageable()
    {
        var query = uow.Repository<Shift>().Query();
        if (currentUser.Role == Roles.SystemAdministrator)
            return query;
        if (currentUser.Role != Roles.FacilityManager)
            return query.Where(_ => false);
        var ids = StaffScope.FacilityIdsOf(uow, currentUser.UserId);
        return query.Where(s => ids.Contains(s.FacilityId));
    }

    /// <summary>Admin and business managers see all; a manager their facilities; a staff member only their own shifts.</summary>
    private IQueryable<Shift> Visible()
    {
        var query = uow.Repository<Shift>().Query();
        if (StaffScope.SeesAllFacilities(currentUser.Role))
            return query;
        if (currentUser.Role == Roles.FacilityManager)
        {
            var ids = StaffScope.FacilityIdsOf(uow, currentUser.UserId);
            return query.Where(s => ids.Contains(s.FacilityId));
        }
        if (currentUser.Role == Roles.FacilityStaff)
        {
            var me = currentUser.UserId;
            return query.Where(s => s.UserId == me);
        }
        return query.Where(_ => false);
    }

    private static IQueryable<ShiftDto> Project(IQueryable<Shift> source) =>
        source.Select(s => new ShiftDto(
            s.ShiftId, s.UserId, s.User.FullName, s.FacilityId, s.Facility.Name,
            s.ShiftDate, s.StartTime, s.EndTime, s.EndTime <= s.StartTime, s.ShiftType));
}
