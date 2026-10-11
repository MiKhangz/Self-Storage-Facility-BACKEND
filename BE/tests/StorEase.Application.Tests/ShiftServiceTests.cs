using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Staff;
using StorEase.Application.Features.Staff.Dtos;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Tests;

public class ShiftServiceTests
{
    private sealed class World
    {
        public required TestDb Db { get; init; }
        public required User Manager { get; init; }
        public required User Staff { get; init; }
        public required User Other { get; init; }
        public required User Admin { get; init; }
        public required Facility F1 { get; init; }
        public required Facility F2 { get; init; }
        public required Facility Foreign { get; init; }

        public ShiftService As(User u) => new(Db.Uow, new FakeCurrentUser(u.UserId, u.Role.RoleName));
    }

    // Far enough ahead that "not in the past" never interferes with the case under test.
    private static readonly DateOnly Day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10);

    private static User NewUser(Role role, string email, UserStatus status = UserStatus.Active) => new()
    {
        Role = role, FullName = email, Email = email, PhoneNumber = "0900000000", PasswordHash = "x", Status = status,
    };

    private static Facility NewFacility(string code) => new()
    {
        Code = code, Name = code, Address = "a", District = "d", Phone = "1", OpeningHours = "8-17", Status = FacilityStatus.Active,
    };

    private static async Task<World> SeedAsync()
    {
        var db = new TestDb();
        var mgrRole = new Role { RoleName = Roles.FacilityManager };
        var staffRole = new Role { RoleName = Roles.FacilityStaff };
        var adminRole = new Role { RoleName = Roles.SystemAdministrator };
        var manager = NewUser(mgrRole, "m@x.vn");
        var staff = NewUser(staffRole, "s@x.vn");
        var other = NewUser(staffRole, "o@x.vn");
        var admin = NewUser(adminRole, "a@x.vn");
        var f1 = NewFacility("F1");
        var f2 = NewFacility("F2");
        var foreign = NewFacility("F3");
        db.Db.AddRange(manager, staff, other, admin, f1, f2, foreign);
        var assigned = new DateOnly(2026, 10, 1);
        db.Db.StaffFacilities.AddRange(
            new StaffFacility { User = manager, Facility = f1, AssignedDate = assigned },
            new StaffFacility { User = manager, Facility = f2, AssignedDate = assigned },
            new StaffFacility { User = staff, Facility = f1, AssignedDate = assigned },
            new StaffFacility { User = staff, Facility = f2, AssignedDate = assigned },
            new StaffFacility { User = other, Facility = f1, AssignedDate = assigned });
        await db.Db.SaveChangesAsync();
        return new World { Db = db, Manager = manager, Staff = staff, Other = other, Admin = admin, F1 = f1, F2 = f2, Foreign = foreign };
    }

    private static CreateShiftRequest Req(User u, Facility f, DateOnly date, int startHour, int endHour) =>
        new(u.UserId, f.FacilityId, date, new TimeOnly(startHour, 0), new TimeOnly(endHour, 0), ShiftType.Morning);

    [Fact]
    public async Task Manager_creates_a_shift_for_staff_of_the_facility()
    {
        var w = await SeedAsync();

        var result = await w.As(w.Manager).CreateAsync(Req(w.Staff, w.F1, Day, 8, 16), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.UserName.Should().Be("s@x.vn");
        result.Value.EndsNextDay.Should().BeFalse();
    }

    [Fact]
    public async Task Overlapping_shift_for_the_same_person_is_a_conflict_even_at_another_facility()
    {
        var w = await SeedAsync();
        var svc = w.As(w.Manager);
        (await svc.CreateAsync(Req(w.Staff, w.F1, Day, 8, 16), default)).IsSuccess.Should().BeTrue();

        var clash = await svc.CreateAsync(Req(w.Staff, w.F2, Day, 15, 20), default);

        clash.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Back_to_back_shifts_do_not_overlap()
    {
        var w = await SeedAsync();
        var svc = w.As(w.Manager);
        (await svc.CreateAsync(Req(w.Staff, w.F1, Day, 8, 16), default)).IsSuccess.Should().BeTrue();

        (await svc.CreateAsync(Req(w.Staff, w.F2, Day, 16, 22), default)).IsSuccess.Should().BeTrue();
        (await svc.CreateAsync(Req(w.Staff, w.F1, Day, 6, 8), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Different_people_may_work_the_same_hours()
    {
        var w = await SeedAsync();
        var svc = w.As(w.Manager);
        (await svc.CreateAsync(Req(w.Staff, w.F1, Day, 8, 16), default)).IsSuccess.Should().BeTrue();

        (await svc.CreateAsync(Req(w.Other, w.F1, Day, 8, 16), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Overnight_shift_ends_the_next_day_and_blocks_the_morning_after()
    {
        var w = await SeedAsync();
        var svc = w.As(w.Manager);
        var night = await svc.CreateAsync(Req(w.Staff, w.F1, Day, 22, 6), default);
        night.Value!.EndsNextDay.Should().BeTrue();

        // 05:00-07:00 the next day starts before the night shift ends.
        var clash = await svc.CreateAsync(Req(w.Staff, w.F2, Day.AddDays(1), 5, 7), default);
        clash.Error!.Type.Should().Be(ErrorType.Conflict);

        // 06:00 onwards is free (boundary), and so is the evening before 22:00.
        (await svc.CreateAsync(Req(w.Staff, w.F2, Day.AddDays(1), 6, 14), default)).IsSuccess.Should().BeTrue();
        (await svc.CreateAsync(Req(w.Staff, w.F2, Day, 14, 22), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Overnight_request_is_rejected_when_it_runs_into_a_shift_the_next_day()
    {
        var w = await SeedAsync();
        var svc = w.As(w.Manager);
        (await svc.CreateAsync(Req(w.Staff, w.F1, Day.AddDays(1), 1, 9), default)).IsSuccess.Should().BeTrue();

        var clash = await svc.CreateAsync(Req(w.Staff, w.F2, Day, 22, 3), default);

        clash.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Cannot_create_a_shift_in_the_past()
    {
        var w = await SeedAsync();
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2);

        var result = await w.As(w.Manager).CreateAsync(Req(w.Staff, w.F1, yesterday, 8, 16), default);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Assignee_must_be_active_staff_of_that_facility()
    {
        var w = await SeedAsync();
        var svc = w.As(w.Manager);

        // Other works only at F1.
        (await svc.CreateAsync(Req(w.Other, w.F2, Day, 8, 16), default)).Error!.Type.Should().Be(ErrorType.Conflict);

        // A failed call clears the change tracker, so update by query rather than through the detached entity.
        await w.Db.Db.Users.Where(u => u.UserId == w.Staff.UserId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, UserStatus.Suspended));
        (await svc.CreateAsync(Req(w.Staff, w.F1, Day, 8, 16), default)).Error!.Type.Should().Be(ErrorType.Conflict);

        (await svc.CreateAsync(new CreateShiftRequest(9999, w.F1.FacilityId, Day, new(8, 0), new(16, 0), ShiftType.Morning), default))
            .Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Manager_cannot_manage_a_facility_outside_their_scope()
    {
        var w = await SeedAsync();

        var result = await w.As(w.Manager).CreateAsync(Req(w.Staff, w.Foreign, Day, 8, 16), default);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Staff_cannot_create_shifts()
    {
        var w = await SeedAsync();

        var result = await w.As(w.Staff).CreateAsync(Req(w.Staff, w.F1, Day, 8, 16), default);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Staff_see_only_their_own_shifts_and_a_manager_those_of_their_facilities()
    {
        var w = await SeedAsync();
        var mgr = w.As(w.Manager);
        var mine = (await mgr.CreateAsync(Req(w.Staff, w.F1, Day, 8, 16), default)).Value!;
        var theirs = (await mgr.CreateAsync(Req(w.Other, w.F1, Day, 8, 16), default)).Value!;

        var staffList = await w.As(w.Staff).ListAsync(null, null, null, null, 1, 20, default);
        staffList.Value!.Items.Select(s => s.ShiftId).Should().Equal(mine.ShiftId);
        (await w.As(w.Staff).GetAsync(theirs.ShiftId, default)).Error!.Type.Should().Be(ErrorType.NotFound);

        var mgrList = await mgr.ListAsync(null, null, null, null, 1, 20, default);
        mgrList.Value!.TotalCount.Should().Be(2);

        var filtered = await mgr.ListAsync(null, w.Other.UserId, Day, Day, 1, 20, default);
        filtered.Value!.Items.Should().ContainSingle().Which.ShiftId.Should().Be(theirs.ShiftId);
    }

    [Fact]
    public async Task Update_moves_a_shift_but_not_onto_another_of_the_same_person()
    {
        var w = await SeedAsync();
        var svc = w.As(w.Manager);
        var a = (await svc.CreateAsync(Req(w.Staff, w.F1, Day, 8, 12), default)).Value!;
        await svc.CreateAsync(Req(w.Staff, w.F1, Day, 14, 18), default);

        // Growing into itself is fine: it is ignored when checking for overlap.
        (await svc.UpdateAsync(a.ShiftId, new(Day, new(8, 0), new(13, 0), ShiftType.Morning), default)).IsSuccess.Should().BeTrue();
        (await svc.UpdateAsync(a.ShiftId, new(Day, new(8, 0), new(15, 0), ShiftType.Morning), default)).Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Delete_removes_a_future_shift_without_tasks_only()
    {
        var w = await SeedAsync();
        var svc = w.As(w.Manager);
        var free = (await svc.CreateAsync(Req(w.Staff, w.F1, Day, 8, 12), default)).Value!;
        var busy = (await svc.CreateAsync(Req(w.Staff, w.F1, Day.AddDays(1), 8, 12), default)).Value!;
        w.Db.Db.StaffTasks.Add(new StaffTask
        {
            ShiftId = busy.ShiftId, AssignedTo = w.Staff.UserId, TaskType = TaskType.Other,
            ScheduledAt = DateTime.UtcNow, Status = StaffTaskStatus.ToDo,
        });
        await w.Db.Db.SaveChangesAsync();

        (await svc.DeleteAsync(busy.ShiftId, default)).Error!.Type.Should().Be(ErrorType.Conflict);
        (await svc.DeleteAsync(free.ShiftId, default)).IsSuccess.Should().BeTrue();
        (await svc.GetAsync(free.ShiftId, default)).Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Shift_that_already_started_cannot_be_deleted_or_changed()
    {
        var w = await SeedAsync();
        var local = DateTime.UtcNow.AddHours(7);
        var started = new Shift
        {
            UserId = w.Staff.UserId, FacilityId = w.F1.FacilityId, ShiftType = ShiftType.Morning,
            ShiftDate = DateOnly.FromDateTime(local.AddDays(-2)), StartTime = new(8, 0), EndTime = new(16, 0),
        };
        w.Db.Db.Shifts.Add(started);
        await w.Db.Db.SaveChangesAsync();
        var svc = w.As(w.Manager);

        (await svc.DeleteAsync(started.ShiftId, default)).Error!.Type.Should().Be(ErrorType.Conflict);
        (await svc.UpdateAsync(started.ShiftId, new(Day, new(8, 0), new(16, 0), ShiftType.Morning), default))
            .Error!.Type.Should().Be(ErrorType.Conflict);
    }
}
