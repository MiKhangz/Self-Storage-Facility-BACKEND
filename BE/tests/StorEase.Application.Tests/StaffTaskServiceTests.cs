using FluentAssertions;
using StorEase.Application.Common;
using StorEase.Application.Features.Staff;
using StorEase.Application.Features.Staff.Dtos;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Tests;

public class StaffTaskServiceTests
{
    private sealed class World
    {
        public required TestDb Db { get; init; }
        public required User Manager { get; init; }
        public required User Staff { get; init; }
        public required User OtherStaff { get; init; }
        public required User OtherManager { get; init; }
        public required Shift DayShift { get; init; }
        public required Shift NightShift { get; init; }

        public StaffTaskService As(User u) => new(Db.Uow, new FakeCurrentUser(u.UserId, u.Role.RoleName));
    }

    private static readonly DateOnly Day = new(2026, 10, 20);

    private static async Task<World> SeedAsync()
    {
        var db = new TestDb();
        var staffRole = new Role { RoleName = Roles.FacilityStaff };
        var managerRole = new Role { RoleName = Roles.FacilityManager };
        var manager = NewUser(managerRole, "m@x.vn");
        var staff = NewUser(staffRole, "s@x.vn");
        var otherStaff = NewUser(staffRole, "s2@x.vn");
        var otherManager = NewUser(managerRole, "m2@x.vn");
        var f1 = NewFacility("F1", manager);
        var f2 = NewFacility("F2", otherManager);
        var day = new Shift { User = staff, Facility = f1, ShiftDate = Day, StartTime = new(8, 0), EndTime = new(16, 0), ShiftType = ShiftType.Morning };
        var night = new Shift { User = staff, Facility = f1, ShiftDate = Day, StartTime = new(22, 0), EndTime = new(6, 0), ShiftType = ShiftType.Night };
        db.Db.AddRange(day, night, otherStaff, f2);
        db.Db.StaffFacilities.AddRange(
            new StaffFacility { User = staff, Facility = f1, AssignedDate = new DateOnly(2026, 10, 1) },
            new StaffFacility { User = otherStaff, Facility = f2, AssignedDate = new DateOnly(2026, 10, 1) });
        await db.Db.SaveChangesAsync();
        return new World
        {
            Db = db, Manager = manager, Staff = staff, OtherStaff = otherStaff, OtherManager = otherManager,
            DayShift = day, NightShift = night,
        };
    }

    // Local (UTC+7) wall clock to a UTC instant.
    private static DateTime Local(int dayOffset, int hour, int minute = 0) =>
        DateTime.SpecifyKind(Day.ToDateTime(new TimeOnly(hour, minute)).AddDays(dayOffset).AddHours(-7), DateTimeKind.Utc);

    private static CreateStaffTaskRequest Req(Shift shift, DateTime at, TaskType type = TaskType.Inspection, string? note = null) =>
        new(shift.ShiftId, shift.UserId, type, "REF1", note, at);

    [Fact]
    public async Task Manager_creates_a_todo_task_inside_the_shift()
    {
        var w = await SeedAsync();

        var result = await w.As(w.Manager).CreateAsync(Req(w.DayShift, Local(0, 9)), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(StaffTaskStatus.ToDo);
        result.Value.FacilityId.Should().Be(w.DayShift.FacilityId);
        result.Value.AssignedTo.Should().Be(w.Staff.UserId);
    }

    [Theory]
    [InlineData(0, 7, 59)]
    [InlineData(0, 16, 0)]
    public async Task Task_outside_the_shift_window_is_rejected(int dayOffset, int hour, int minute)
    {
        var w = await SeedAsync();

        var result = await w.As(w.Manager).CreateAsync(Req(w.DayShift, Local(dayOffset, hour, minute)), default);

        result.Error!.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Overnight_shift_accepts_times_after_midnight_but_not_after_its_end()
    {
        var w = await SeedAsync();
        var svc = w.As(w.Manager);

        (await svc.CreateAsync(Req(w.NightShift, Local(0, 23)), default)).IsSuccess.Should().BeTrue();
        (await svc.CreateAsync(Req(w.NightShift, Local(1, 5, 30)), default)).IsSuccess.Should().BeTrue();
        (await svc.CreateAsync(Req(w.NightShift, Local(1, 6)), default)).Error!.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Assignee_must_be_the_person_on_the_shift()
    {
        var w = await SeedAsync();

        var result = await w.As(w.Manager).CreateAsync(Req(w.DayShift, Local(0, 9)) with { AssignedTo = w.OtherStaff.UserId }, default);

        result.Error!.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Manager_cannot_use_a_shift_of_another_facility()
    {
        var w = await SeedAsync();

        var result = await w.As(w.OtherManager).CreateAsync(Req(w.DayShift, Local(0, 9)), default);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Assignee_moves_todo_to_inprogress_to_done_and_cannot_skip()
    {
        var w = await SeedAsync();
        var task = (await w.As(w.Manager).CreateAsync(Req(w.DayShift, Local(0, 9)), default)).Value!;
        var svc = w.As(w.Staff);

        (await svc.ChangeStatusAsync(task.TaskId, new(StaffTaskStatus.Done), default)).Error!.Type.Should().Be(ErrorType.Conflict);
        (await svc.ChangeStatusAsync(task.TaskId, new(StaffTaskStatus.InProgress), default)).Value!.Status.Should().Be(StaffTaskStatus.InProgress);
        (await svc.ChangeStatusAsync(task.TaskId, new(StaffTaskStatus.Done), default)).Value!.Status.Should().Be(StaffTaskStatus.Done);
    }

    [Fact]
    public async Task Only_the_assignee_reports_progress()
    {
        var w = await SeedAsync();
        var task = (await w.As(w.Manager).CreateAsync(Req(w.DayShift, Local(0, 9)), default)).Value!;

        var byManager = await w.As(w.Manager).ChangeStatusAsync(task.TaskId, new(StaffTaskStatus.InProgress), default);
        var byOther = await w.As(w.OtherStaff).ChangeStatusAsync(task.TaskId, new(StaffTaskStatus.InProgress), default);

        byManager.Error!.Type.Should().Be(ErrorType.Forbidden);
        byOther.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Only_a_manager_cancels_and_not_a_finished_task()
    {
        var w = await SeedAsync();
        var task = (await w.As(w.Manager).CreateAsync(Req(w.DayShift, Local(0, 9)), default)).Value!;

        (await w.As(w.Staff).ChangeStatusAsync(task.TaskId, new(StaffTaskStatus.Cancelled), default)).Error!.Type.Should().Be(ErrorType.Forbidden);
        (await w.As(w.OtherManager).ChangeStatusAsync(task.TaskId, new(StaffTaskStatus.Cancelled), default)).Error!.Type.Should().Be(ErrorType.NotFound);
        (await w.As(w.Manager).ChangeStatusAsync(task.TaskId, new(StaffTaskStatus.Cancelled), default)).Value!.Status.Should().Be(StaffTaskStatus.Cancelled);
        (await w.As(w.Manager).ChangeStatusAsync(task.TaskId, new(StaffTaskStatus.Cancelled), default)).Error!.Type.Should().Be(ErrorType.Conflict);

        var done = (await w.As(w.Manager).CreateAsync(Req(w.DayShift, Local(0, 10)), default)).Value!;
        await w.As(w.Staff).ChangeStatusAsync(done.TaskId, new(StaffTaskStatus.InProgress), default);
        await w.As(w.Staff).ChangeStatusAsync(done.TaskId, new(StaffTaskStatus.Done), default);
        (await w.As(w.Manager).ChangeStatusAsync(done.TaskId, new(StaffTaskStatus.Cancelled), default)).Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task List_shows_staff_only_their_own_tasks_and_managers_their_facilities()
    {
        var w = await SeedAsync();
        await w.As(w.Manager).CreateAsync(Req(w.DayShift, Local(0, 9)), default);

        var own = await w.As(w.Staff).ListAsync(null, null, null, 1, 20, default);
        var other = await w.As(w.OtherStaff).ListAsync(null, null, null, 1, 20, default);
        var manager = await w.As(w.Manager).ListAsync(null, null, null, 1, 20, default);
        var otherManager = await w.As(w.OtherManager).ListAsync(null, null, null, 1, 20, default);

        own.Value!.Items.Should().HaveCount(1);
        manager.Value!.Items.Should().HaveCount(1);
        other.Value!.Items.Should().BeEmpty();
        otherManager.Value!.Items.Should().BeEmpty();
    }

    private static User NewUser(Role role, string email) => new()
    {
        Role = role, FullName = "Test", Email = email, PasswordHash = "x", Status = UserStatus.Active, CreatedAt = DateTime.UtcNow,
    };

    private static Facility NewFacility(string code, User? manager = null) => new()
    {
        Code = code, Name = code, Address = "addr", District = "d", Status = FacilityStatus.Active, Manager = manager,
    };
}
