using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Staff;
using StorEase.Application.Features.Staff.Dtos;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Tests;

/// <summary>The weekly timetable: a usual week of morning/afternoon slots that becomes real shifts two weeks ahead.</summary>
public class TimetableTests
{
    private static readonly DateOnly Today = VietnamTime.Today;
    private static DateTime NowVn => DateTime.UtcNow.AddHours(7);

    private sealed record World(TestDb Db, User Manager, User Staff, Facility F1, Facility F2)
    {
        public ShiftService As(User u) => new(Db.Uow, new FakeCurrentUser(u.UserId, u.Role.RoleName));
        public Task<List<Shift>> ShiftsAsync() => Db.Db.Shifts.AsNoTracking().Where(s => s.UserId == Staff.UserId).ToListAsync();
    }

    private static async Task<World> SeedAsync()
    {
        var db = new TestDb();
        var mgrRole = new Role { RoleName = Roles.FacilityManager };
        var staffRole = new Role { RoleName = Roles.FacilityStaff };
        User Person(Role r, string e) => new() { Role = r, FullName = e, Email = e, PhoneNumber = "0900000000", PasswordHash = "x", Status = UserStatus.Active };
        Facility Place(string c) => new() { Code = c, Name = c, Address = "a", District = "d", Phone = "1", OpeningHours = "07-21", Status = FacilityStatus.Active };
        var manager = Person(mgrRole, "m@x.vn");
        var staff = Person(staffRole, "s@x.vn");
        var f1 = Place("F1");
        var f2 = Place("F2");
        db.Db.AddRange(manager, staff, f1, f2);
        var on = new DateOnly(2026, 10, 1);
        db.Db.StaffFacilities.AddRange(
            new StaffFacility { User = manager, Facility = f1, AssignedDate = on },
            new StaffFacility { User = manager, Facility = f2, AssignedDate = on },
            new StaffFacility { User = staff, Facility = f1, AssignedDate = on },
            new StaffFacility { User = staff, Facility = f2, AssignedDate = on });
        await db.Db.SaveChangesAsync();
        return new World(db, manager, staff, f1, f2);
    }

    private static SaveTimetableRequest Week(World w, Facility f, ShiftType type, params DayOfWeek[] days) =>
        new(w.Staff.UserId, f.FacilityId, days.Select(d => new TimetableSlot(d, type)).ToList());

    /// <summary>The days of the next two weeks a slot becomes a shift: right weekday, not yet begun.</summary>
    private static List<DateOnly> Expected(int startHour, params DayOfWeek[] days) =>
        Enumerable.Range(0, ShiftService.TimetableDays).Select(Today.AddDays)
            .Where(d => days.Contains(d.DayOfWeek) && d.ToDateTime(new TimeOnly(startHour, 0)) >= NowVn).ToList();

    private static readonly DayOfWeek[] MonWedFri = [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday];

    [Fact]
    public async Task Saving_mon_wed_fri_mornings_creates_those_shifts_for_two_weeks()
    {
        var w = await SeedAsync();

        var saved = await w.As(w.Manager).SaveTimetableAsync(Week(w, w.F1, ShiftType.Morning, MonWedFri), default);

        saved.IsSuccess.Should().BeTrue();
        saved.Value!.Slots.Should().HaveCount(3);
        saved.Value.Through.Should().Be(Today.AddDays(ShiftService.TimetableDays - 1));
        var shifts = await w.ShiftsAsync();
        shifts.Select(s => s.ShiftDate).Should().BeEquivalentTo(Expected(7, MonWedFri));
        shifts.Should().OnlyContain(s => s.FromTimetable && s.StartTime == new TimeOnly(7, 0) && s.EndTime == new TimeOnly(15, 0));
        (await w.As(w.Manager).GetTimetableAsync(w.Staff.UserId, w.F1.FacilityId, default)).Value!.Slots
            .Select(s => s.Day).Should().Equal(MonWedFri);
    }

    [Fact]
    public async Task A_night_slot_or_a_staff_member_of_another_facility_is_refused()
    {
        var w = await SeedAsync();
        var svc = w.As(w.Manager);

        (await svc.SaveTimetableAsync(Week(w, w.F1, ShiftType.Night, DayOfWeek.Monday), default))
            .Error!.Type.Should().Be(ErrorType.Validation);
        (await svc.SaveTimetableAsync(new(w.Manager.UserId + 99, w.F1.FacilityId, []), default))
            .Error!.Type.Should().Be(ErrorType.NotFound);
        (await w.As(w.Staff).SaveTimetableAsync(Week(w, w.F1, ShiftType.Morning, DayOfWeek.Monday), default))
            .Error!.Type.Should().Be(ErrorType.NotFound);
        (await w.ShiftsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Changing_the_timetable_drops_its_own_shifts_but_keeps_ones_edited_by_hand_or_with_tasks()
    {
        var w = await SeedAsync();
        var svc = w.As(w.Manager);
        var everyDay = Enum.GetValues<DayOfWeek>();
        await svc.SaveTimetableAsync(Week(w, w.F1, ShiftType.Afternoon, everyDay), default);
        var made = (await w.ShiftsAsync()).OrderBy(s => s.ShiftDate).ToList();
        made.Should().HaveCountGreaterThan(10);
        var edited = made[^1];
        var tasked = made[^2];
        (await svc.UpdateAsync(edited.ShiftId, new(edited.ShiftDate, new(16, 0), new(22, 0), ShiftType.Afternoon), default)).IsSuccess.Should().BeTrue();
        w.Db.Db.StaffTasks.Add(new StaffTask
        {
            ShiftId = tasked.ShiftId, AssignedTo = w.Staff.UserId, TaskType = TaskType.Other, ScheduledAt = DateTime.UtcNow, Status = StaffTaskStatus.ToDo,
        });
        await w.Db.Db.SaveChangesAsync();

        (await svc.SaveTimetableAsync(Week(w, w.F1, ShiftType.Afternoon, []), default)).IsSuccess.Should().BeTrue();

        (await w.ShiftsAsync()).Select(s => s.ShiftId).Should().BeEquivalentTo([edited.ShiftId, tasked.ShiftId]);
    }

    [Fact]
    public async Task A_slot_clashing_with_a_shift_at_another_facility_is_skipped_and_reported()
    {
        var w = await SeedAsync();
        var svc = w.As(w.Manager);
        var day = Today.AddDays(7);
        (await svc.CreateAsync(new(w.Staff.UserId, w.F2.FacilityId, day, new(8, 0), new(12, 0), ShiftType.Morning), default)).IsSuccess.Should().BeTrue();

        var saved = await svc.SaveTimetableAsync(Week(w, w.F1, ShiftType.Morning, day.DayOfWeek), default);

        saved.Value!.Skipped.Should().ContainSingle().Which.Should().Contain("Overlaps").And.Contain("F2");
        (await w.ShiftsAsync()).Where(s => s.FacilityId == w.F1.FacilityId).Select(s => s.ShiftDate)
            .Should().BeEquivalentTo(Expected(7, day.DayOfWeek).Where(d => d != day));
    }

    [Fact]
    public async Task The_job_keeps_two_weeks_ahead_and_a_day_off_does_not_come_back()
    {
        var w = await SeedAsync();
        var svc = w.As(w.Manager);
        await svc.SaveTimetableAsync(Week(w, w.F1, ShiftType.Afternoon, MonWedFri), default);
        var dayOff = (await w.ShiftsAsync()).OrderBy(s => s.ShiftDate).Last();
        (await svc.DeleteAsync(dayOff.ShiftId, default)).IsSuccess.Should().BeTrue();
        // As if the last save was three days ago: the job must add the days since.
        var link = await w.Db.Db.StaffFacilities.FirstAsync(s => s.UserId == w.Staff.UserId && s.FacilityId == w.F1.FacilityId);
        link.TimetableThrough = Today.AddDays(ShiftService.TimetableDays - 4);
        w.Db.Db.Shifts.RemoveRange(w.Db.Db.Shifts.Where(s => s.ShiftDate > link.TimetableThrough));
        await w.Db.Db.SaveChangesAsync();

        var made = await svc.ExtendTimetablesAsync(default);

        var expected = Expected(15, MonWedFri);
        made.Should().Be(expected.Count(d => d > Today.AddDays(ShiftService.TimetableDays - 4)));
        (await w.ShiftsAsync()).Select(s => s.ShiftDate).Should().BeEquivalentTo(expected);
        (await svc.ExtendTimetablesAsync(default)).Should().Be(0);
    }
}
