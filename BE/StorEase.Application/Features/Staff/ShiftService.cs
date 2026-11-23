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
    Task<Result<TimetableDto>> GetTimetableAsync(int userId, int facilityId, CancellationToken ct);
    Task<Result<TimetableDto>> SaveTimetableAsync(SaveTimetableRequest request, CancellationToken ct);
    /// <summary>The job: keeps every timetable's shifts created <see cref="ShiftService.TimetableDays"/> days ahead.</summary>
    Task<int> ExtendTimetablesAsync(CancellationToken ct);
}

public class ShiftService(IUnitOfWork uow, ICurrentUser currentUser) : IShiftService
{
    /// <summary>How far ahead a timetable's shifts exist, so the roster never runs out.</summary>
    public const int TimetableDays = 14;

    /// <summary>The timetable's two slots, the same hours as the usual shifts on the roster.</summary>
    public static readonly IReadOnlyDictionary<ShiftType, (TimeOnly Start, TimeOnly End)> SlotTimes =
        new Dictionary<ShiftType, (TimeOnly, TimeOnly)>
        {
            [ShiftType.Morning] = (new TimeOnly(7, 0), new TimeOnly(15, 0)),
            [ShiftType.Afternoon] = (new TimeOnly(15, 0), new TimeOnly(23, 0)),
        };

    public async Task<Result<ShiftDto>> CreateAsync(CreateShiftRequest request, CancellationToken ct)
    {
        var created = await uow.InTransactionAsync<int>(async () =>
        {
            if (!await CanManageAsync(request.FacilityId, ct))
                return Error.NotFound("Facility not found.");

            if (await AssigneeErrorAsync(request.UserId, request.FacilityId, ct) is { } wrong)
                return wrong;

            if (Start(request.ShiftDate, request.StartTime) < VietnamTime.Now)
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
            if (End(shift.ShiftDate, shift.StartTime, shift.EndTime) <= VietnamTime.Now)
                return Error.Conflict("A shift that has already ended cannot be changed.");
            if (Start(request.ShiftDate, request.StartTime) < VietnamTime.Now)
                return Error.Conflict("A shift cannot be moved into the past.");

            var clash = await FindOverlapAsync(shift.UserId, shift.ShiftId, request.ShiftDate, request.StartTime, request.EndTime, ct);
            if (clash is not null)
                return clash;

            shift.ShiftDate = request.ShiftDate;
            shift.StartTime = request.StartTime;
            shift.EndTime = request.EndTime;
            shift.ShiftType = request.ShiftType;
            // Changed by hand: a new timetable leaves it alone.
            shift.FromTimetable = false;
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
            if (Start(shift.ShiftDate, shift.StartTime) <= VietnamTime.Now)
                return Error.Conflict("A shift that has started cannot be deleted.");
            if (await uow.Repository<StaffTask>().Query().AnyAsync(t => t.ShiftId == shiftId, ct))
                return Error.Conflict("This shift has tasks; remove or cancel them first.");

            uow.Repository<Shift>().Remove(shift);
            return true;
        }, ct);

    public async Task<Result<TimetableDto>> GetTimetableAsync(int userId, int facilityId, CancellationToken ct)
    {
        if (!await CanManageAsync(facilityId, ct))
            return Error.NotFound("Facility not found.");
        var link = await uow.Repository<StaffFacility>().Query().AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.FacilityId == facilityId, ct);
        return link is null
            ? Error.Conflict("This person is not assigned to the facility.")
            : new TimetableDto(userId, facilityId, ParseTimetable(link.Timetable), link.TimetableThrough, []);
    }

    public async Task<Result<TimetableDto>> SaveTimetableAsync(SaveTimetableRequest request, CancellationToken ct) =>
        await uow.InTransactionAsync<TimetableDto>(async () =>
        {
            if (!await CanManageAsync(request.FacilityId, ct))
                return Error.NotFound("Facility not found.");
            if (await AssigneeErrorAsync(request.UserId, request.FacilityId, ct) is { } wrong)
                return wrong;
            var slots = (request.Slots ?? []).Distinct()
                .OrderBy(s => ((int)s.Day + 6) % 7).ThenBy(s => s.ShiftType).ToList();
            if (slots.Any(s => !SlotTimes.ContainsKey(s.ShiftType) || !Enum.IsDefined(s.Day)))
                return Error.Validation("The timetable only has morning and afternoon slots.");

            var link = await uow.Repository<StaffFacility>().Query()
                .FirstAsync(s => s.UserId == request.UserId && s.FacilityId == request.FacilityId, ct);
            link.Timetable = FormatTimetable(slots);

            // Rebuild from today: drop the timetable's own future shifts that nobody changed and that carry no tasks.
            // shortcut: a day off made by deleting one of these shifts comes back when the timetable is saved again; add a skip list if managers need it.
            var today = VietnamTime.Today;
            var now = VietnamTime.Now;
            var withTasks = uow.Repository<StaffTask>().Query().Where(t => t.ShiftId != null).Select(t => t.ShiftId!.Value);
            var old = await uow.Repository<Shift>().Query()
                .Where(s => s.UserId == request.UserId && s.FacilityId == request.FacilityId && s.FromTimetable
                    && s.ShiftDate >= today && !withTasks.Contains(s.ShiftId))
                .ToListAsync(ct);
            foreach (var shift in old.Where(s => Start(s.ShiftDate, s.StartTime) > now))
                uow.Repository<Shift>().Remove(shift);
            await uow.SaveChangesAsync(ct);

            link.TimetableThrough = today.AddDays(-1);
            var (_, skipped) = await FillAsync(link, slots, today.AddDays(TimetableDays - 1), ct);
            await uow.SaveChangesAsync(ct);
            return new TimetableDto(request.UserId, request.FacilityId, slots, link.TimetableThrough, skipped);
        }, ct);

    public async Task<int> ExtendTimetablesAsync(CancellationToken ct)
    {
        var until = VietnamTime.Today.AddDays(TimetableDays - 1);
        var links = await uow.Repository<StaffFacility>().Query()
            .Where(s => s.Timetable != null && (s.TimetableThrough == null || s.TimetableThrough < until)
                && s.User.Status == UserStatus.Active
                && (s.User.Role.RoleName == Roles.FacilityStaff || s.User.Role.RoleName == Roles.FacilityManager))
            .ToListAsync(ct);
        var made = 0;
        foreach (var link in links)
            made += (await FillAsync(link, ParseTimetable(link.Timetable), until, ct)).Created;
        await uow.SaveChangesAsync(ct);
        return made;
    }

    /// <summary>
    /// Creates the timetable's shifts from the day after <c>TimetableThrough</c> to <paramref name="until"/>.
    /// A slot already begun, already there, or clashing with another shift of the person is skipped; clashes are reported.
    /// </summary>
    private async Task<(int Created, List<string> Skipped)> FillAsync(StaffFacility link, IReadOnlyList<TimetableSlot> slots, DateOnly until, CancellationToken ct)
    {
        var created = 0;
        var skipped = new List<string>();
        var today = VietnamTime.Today;
        var from = link.TimetableThrough is { } done && done >= today ? done.AddDays(1) : today;
        var now = VietnamTime.Now;
        for (var day = from; day <= until; day = day.AddDays(1))
            foreach (var slot in slots.Where(s => s.Day == day.DayOfWeek))
            {
                var (start, end) = SlotTimes[slot.ShiftType];
                if (Start(day, start) < now || await uow.Repository<Shift>().Query().AnyAsync(s =>
                        s.UserId == link.UserId && s.FacilityId == link.FacilityId && s.ShiftDate == day && s.StartTime == start, ct))
                    continue;
                if (await FindOverlapAsync(link.UserId, null, day, start, end, ct) is { } clash)
                {
                    skipped.Add($"{day:dd/MM} {slot.ShiftType}: {clash.Message}");
                    continue;
                }
                uow.Repository<Shift>().Add(new Shift
                {
                    UserId = link.UserId, FacilityId = link.FacilityId, ShiftDate = day,
                    StartTime = start, EndTime = end, ShiftType = slot.ShiftType, FromTimetable = true,
                });
                created++;
                // Saved at once, so the next slot's clash check sees this shift.
                await uow.SaveChangesAsync(ct);
            }
        link.TimetableThrough = until;
        return (created, skipped);
    }

    /// <summary>"Mon:Morning,Wed:Afternoon": short day names, so a full week (14 slots, 181 characters) fits the 200-character column.</summary>
    public static string? FormatTimetable(IReadOnlyCollection<TimetableSlot> slots) =>
        slots.Count == 0 ? null : string.Join(",", slots.Select(s => $"{s.Day.ToString()[..3]}:{s.ShiftType}"));

    private static IReadOnlyList<TimetableSlot> ParseTimetable(string? text) =>
        string.IsNullOrEmpty(text) ? [] : text.Split(',').Select(cell => cell.Split(':'))
            .Select(p => new TimetableSlot(Enum.GetValues<DayOfWeek>().First(d => d.ToString()[..3] == p[0][..3]), Enum.Parse<ShiftType>(p[1])))
            .ToList();

    private async Task<Error?> AssigneeErrorAsync(int userId, int facilityId, CancellationToken ct)
    {
        var assignee = await uow.Repository<User>().Query()
            .Where(u => u.UserId == userId)
            .Select(u => new { u.Status, Role = u.Role.RoleName })
            .FirstOrDefaultAsync(ct);
        if (assignee is null)
            return Error.NotFound("Staff member not found.");
        if (assignee.Status != UserStatus.Active || assignee.Role is not (Roles.FacilityStaff or Roles.FacilityManager))
            return Error.Conflict("Only an Active facility staff member or manager can be given a shift.");
        if (!await uow.Repository<StaffFacility>().Query().AnyAsync(s => s.UserId == userId && s.FacilityId == facilityId, ct))
            return Error.Conflict("This person is not assigned to the facility.");
        return null;
    }

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
