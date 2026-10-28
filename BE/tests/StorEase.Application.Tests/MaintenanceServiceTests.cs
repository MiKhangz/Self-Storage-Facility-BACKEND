using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Maintenance;
using StorEase.Application.Features.Maintenance.Dtos;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Tests;

public class MaintenanceServiceTests
{
    private sealed class World
    {
        public required TestDb Db { get; init; }
        public required User Staff { get; init; }
        public required User Outside { get; init; }
        public required User Admin { get; init; }
        public required Facility Facility { get; init; }
        public required StorageUnit Unit { get; init; }
        public required StorageUnit OccupiedUnit { get; init; }

        public MaintenanceService As(User u) => new(Db.Uow, new FakeCurrentUser(u.UserId, u.Role.RoleName));
        public Task<StorageUnitStatus> UnitStatus(StorageUnit u) =>
            Db.Db.StorageUnits.AsNoTracking().Where(x => x.StorageUnitId == u.StorageUnitId).Select(x => x.Status).SingleAsync();
    }

    private static async Task<World> SeedAsync()
    {
        var db = new TestDb();
        var staffRole = new Role { RoleName = Roles.FacilityStaff };
        var adminRole = new Role { RoleName = Roles.SystemAdministrator };
        var staff = NewUser(staffRole, "s@x.vn");
        var outside = NewUser(staffRole, "s2@x.vn");
        var admin = NewUser(adminRole, "a@x.vn");
        var facility = NewFacility("F1");
        var elsewhere = NewFacility("F2");
        var floor = new Floor { Facility = facility, FloorNumber = 1 };
        var type = new UnitType { Code = "S", Name = "Small" };
        var unit = new StorageUnit { Facility = facility, Floor = floor, UnitType = type, UnitCode = "A1", Status = StorageUnitStatus.Available };
        var occupied = new StorageUnit { Facility = facility, Floor = floor, UnitType = type, UnitCode = "A2", Status = StorageUnitStatus.Occupied };
        db.Db.AddRange(staff, outside, admin, elsewhere, unit, occupied);
        db.Db.StaffFacilities.AddRange(
            new StaffFacility { User = staff, Facility = facility, AssignedDate = new DateOnly(2026, 10, 1) },
            new StaffFacility { User = outside, Facility = elsewhere, AssignedDate = new DateOnly(2026, 10, 1) });
        await db.Db.SaveChangesAsync();
        return new World { Db = db, Staff = staff, Outside = outside, Admin = admin, Facility = facility, Unit = unit, OccupiedUnit = occupied };
    }

    private static CreateMaintenanceOrderRequest Req(StorageUnit unit, DateTime? at = null) =>
        new("Replace lock", unit.StorageUnitId, null, at, null);

    private static ChangeMaintenanceStatusRequest To(MaintenanceOrderStatus s) => new(s, null, null, null);

    private static ChangeMaintenanceStatusRequest Done() => new(MaintenanceOrderStatus.Done, null, 150_000m, "Lock replaced");

    [Fact]
    public async Task Create_starts_open_or_scheduled_depending_on_date()
    {
        var w = await SeedAsync();
        using var _ = w.Db;

        var open = await w.As(w.Staff).CreateAsync(Req(w.Unit), default);
        var scheduled = await w.As(w.Staff).CreateAsync(Req(w.Unit, DateTime.UtcNow.AddDays(2)), default);

        open.Value.Status.Should().Be(MaintenanceOrderStatus.Open);
        scheduled.Value.Status.Should().Be(MaintenanceOrderStatus.Scheduled);
    }

    [Fact]
    public async Task Create_from_ticket_takes_the_unit_and_rejects_ticket_without_unit()
    {
        var w = await SeedAsync();
        using var _ = w.Db;
        var customer = NewUser(new Role { RoleName = Roles.StorageCustomer }, "c@x.vn");
        var category = new TicketCategory { Name = "Lock", DefaultPriority = Priority.High };
        var withUnit = NewTicket(customer, category, w.Facility, w.Unit, "TK1");
        var withoutUnit = NewTicket(customer, category, w.Facility, null, "TK2");
        w.Db.Db.AddRange(withUnit, withoutUnit);
        await w.Db.Db.SaveChangesAsync();

        var ok = await w.As(w.Staff).CreateAsync(new("Fix", null, withUnit.TicketId, null, null), default);
        var bad = await w.As(w.Staff).CreateAsync(new("Fix", null, withoutUnit.TicketId, null, null), default);

        ok.Value.StorageUnitId.Should().Be(w.Unit.StorageUnitId);
        ok.Value.TicketId.Should().Be(withUnit.TicketId);
        bad.Error!.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Staff_outside_the_facility_gets_404_but_admin_sees_it()
    {
        var w = await SeedAsync();
        using var _ = w.Db;

        (await w.As(w.Outside).CreateAsync(Req(w.Unit), default)).Error!.Type.Should().Be(ErrorType.NotFound);
        var id = (await w.As(w.Staff).CreateAsync(Req(w.Unit), default)).Value.MaintenanceOrderId;

        (await w.As(w.Outside).GetAsync(id, default)).Error!.Type.Should().Be(ErrorType.NotFound);
        (await w.As(w.Outside).ChangeStatusAsync(id, To(MaintenanceOrderStatus.Cancelled), default)).Error!.Type.Should().Be(ErrorType.NotFound);
        (await w.As(w.Admin).GetAsync(id, default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Assignee_must_be_active_staff_of_the_facility()
    {
        var w = await SeedAsync();
        using var _ = w.Db;

        var good = await w.As(w.Staff).CreateAsync(Req(w.Unit) with { AssignedTo = w.Staff.UserId }, default);
        var bad = await w.As(w.Staff).CreateAsync(Req(w.Unit) with { AssignedTo = w.Outside.UserId }, default);

        good.Value.AssignedTo.Should().Be(w.Staff.UserId);
        bad.Error!.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Starting_work_takes_a_free_unit_into_maintenance_and_done_returns_it()
    {
        var w = await SeedAsync();
        using var _ = w.Db;
        var svc = w.As(w.Staff);
        var id = (await svc.CreateAsync(Req(w.Unit), default)).Value.MaintenanceOrderId;

        (await svc.ChangeStatusAsync(id, To(MaintenanceOrderStatus.InProgress), default)).IsSuccess.Should().BeTrue();
        (await w.UnitStatus(w.Unit)).Should().Be(StorageUnitStatus.Maintenance);

        var done = await svc.ChangeStatusAsync(id, Done(), default);

        done.Value.Status.Should().Be(MaintenanceOrderStatus.Done);
        done.Value.Cost.Should().Be(150_000m);
        done.Value.WorkNote.Should().Be("Lock replaced");
        done.Value.CompletedAt.Should().NotBeNull();
        (await w.UnitStatus(w.Unit)).Should().Be(StorageUnitStatus.Available);
    }

    [Fact]
    public async Task A_rented_unit_gets_an_order_only_from_its_customers_ticket_and_keeps_its_status()
    {
        var w = await SeedAsync();
        using var _ = w.Db;
        var svc = w.As(w.Staff);
        var customerRole = new Role { RoleName = Roles.StorageCustomer };
        var renter = NewUser(customerRole, "renter@x.vn");
        var previous = NewUser(customerRole, "previous@x.vn");
        var leak = new TicketCategory { Name = "Leak", DefaultPriority = Priority.High };
        var ticket = NewTicket(renter, leak, w.Facility, w.OccupiedUnit, "TK9");
        var oldTicket = NewTicket(previous, leak, w.Facility, w.OccupiedUnit, "TK1");
        oldTicket.Status = SupportTicketStatus.Closed;
        w.Db.Db.AddRange(ticket, oldTicket, new Contract
        {
            Reservation = new Reservation
            {
                Customer = renter, Facility = w.Facility, UnitType = w.OccupiedUnit.UnitType, StorageUnit = w.OccupiedUnit,
                ReservationCode = "RS-A2", MoveInDate = new DateOnly(2026, 10, 1), RentalMonths = 3,
                Status = ReservationStatus.HandedOver, CreatedAt = DateTime.UtcNow,
            },
            Customer = renter, StorageUnit = w.OccupiedUnit, ContractNumber = "CT-A2", StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2027, 1, 1), MonthlyRent = 1m, DepositAmount = 1m, PaymentDay = 1, Status = ContractStatus.Active,
        });
        await w.Db.Db.SaveChangesAsync();

        var direct = await svc.CreateAsync(Req(w.OccupiedUnit), default);
        direct.Error!.Type.Should().Be(ErrorType.Conflict, "staff cannot open work on a customer's unit on their own");
        var fromOldRenter = await svc.CreateAsync(new("Fix", null, oldTicket.TicketId, null, null), default);
        fromOldRenter.Error!.Type.Should().Be(ErrorType.Conflict, "the previous renter's closed ticket does not speak for the new one");
        var id = (await svc.CreateAsync(new("Fix the leak", null, ticket.TicketId, null, null), default)).Value.MaintenanceOrderId;

        await svc.ChangeStatusAsync(id, To(MaintenanceOrderStatus.InProgress), default);
        (await w.UnitStatus(w.OccupiedUnit)).Should().Be(StorageUnitStatus.Occupied);
        await svc.ChangeStatusAsync(id, Done(), default);

        (await w.UnitStatus(w.OccupiedUnit)).Should().Be(StorageUnitStatus.Occupied);
    }

    [Fact]
    public async Task Unit_stays_in_maintenance_while_another_order_is_still_live()
    {
        var w = await SeedAsync();
        using var _ = w.Db;
        var svc = w.As(w.Staff);
        var first = (await svc.CreateAsync(Req(w.Unit), default)).Value.MaintenanceOrderId;
        var second = (await svc.CreateAsync(Req(w.Unit), default)).Value.MaintenanceOrderId;
        await svc.ChangeStatusAsync(first, To(MaintenanceOrderStatus.InProgress), default);

        await svc.ChangeStatusAsync(first, To(MaintenanceOrderStatus.Cancelled), default);
        (await w.UnitStatus(w.Unit)).Should().Be(StorageUnitStatus.Maintenance);

        // The second order never started, so it cannot tell whether the hold is its own to release.
        await svc.ChangeStatusAsync(second, To(MaintenanceOrderStatus.InProgress), default);
        await svc.ChangeStatusAsync(second, To(MaintenanceOrderStatus.Cancelled), default);
        (await w.UnitStatus(w.Unit)).Should().Be(StorageUnitStatus.Available);
    }

    [Fact]
    public async Task Illegal_transitions_are_409()
    {
        var w = await SeedAsync();
        using var _ = w.Db;
        var svc = w.As(w.Staff);
        var id = (await svc.CreateAsync(Req(w.Unit), default)).Value.MaintenanceOrderId;

        (await svc.ChangeStatusAsync(id, Done(), default)).Error!.Type.Should().Be(ErrorType.Conflict);
        await svc.ChangeStatusAsync(id, To(MaintenanceOrderStatus.InProgress), default);
        await svc.ChangeStatusAsync(id, Done(), default);
        (await svc.ChangeStatusAsync(id, To(MaintenanceOrderStatus.Cancelled), default)).Error!.Type.Should().Be(ErrorType.Conflict);
        (await svc.ChangeStatusAsync(id, To(MaintenanceOrderStatus.InProgress), default)).Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Scheduling_sets_the_date_and_list_filters_by_status()
    {
        var w = await SeedAsync();
        using var _ = w.Db;
        var svc = w.As(w.Staff);
        var id = (await svc.CreateAsync(Req(w.Unit), default)).Value.MaintenanceOrderId;
        await svc.CreateAsync(Req(w.OccupiedUnit), default);
        var at = DateTime.UtcNow.AddDays(3);

        var scheduled = await svc.ChangeStatusAsync(id, new(MaintenanceOrderStatus.Scheduled, at, null, null), default);
        var list = await svc.ListAsync(MaintenanceOrderStatus.Scheduled, null, 1, 20, default);

        scheduled.Value.ScheduledAt.Should().BeCloseTo(at, TimeSpan.FromSeconds(1));
        list.Value.Items.Should().ContainSingle().Which.MaintenanceOrderId.Should().Be(id);
    }

    private static User NewUser(Role role, string email) => new()
    {
        Role = role, FullName = "Test", Email = email, PasswordHash = "x", Status = UserStatus.Active, CreatedAt = DateTime.UtcNow,
    };

    private static Facility NewFacility(string code) => new()
    {
        Code = code, Name = code, Address = "addr", District = "d", Status = FacilityStatus.Active,
    };

    private static SupportTicket NewTicket(User customer, TicketCategory category, Facility facility, StorageUnit? unit, string code) => new()
    {
        Customer = customer, Category = category, Facility = facility, StorageUnit = unit, TicketCode = code,
        Subject = "s", Priority = Priority.Normal, Status = SupportTicketStatus.New, CreatedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task Cancelling_an_order_that_never_started_leaves_a_unit_held_for_another_reason()
    {
        var w = await SeedAsync();
        using var _ = w.Db;
        var svc = w.As(w.Staff);
        var id = (await svc.CreateAsync(Req(w.Unit), default)).Value.MaintenanceOrderId;
        // e.g. a return inspection found damage and parked the unit in Maintenance.
        (await w.Db.Db.StorageUnits.SingleAsync(u => u.StorageUnitId == w.Unit.StorageUnitId)).Status = StorageUnitStatus.Maintenance;
        await w.Db.Db.SaveChangesAsync();

        await svc.ChangeStatusAsync(id, To(MaintenanceOrderStatus.Cancelled), default);

        (await w.UnitStatus(w.Unit)).Should().Be(StorageUnitStatus.Maintenance);
    }

    [Fact]
    public async Task Offsetless_schedule_time_is_read_as_vietnam_time_whatever_the_server_zone()
    {
        var w = await SeedAsync();
        using var _ = w.Db;
        var at = new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Unspecified);

        var created = await w.As(w.Staff).CreateAsync(Req(w.Unit, at), default);
        var id = (await w.As(w.Staff).CreateAsync(Req(w.Unit), default)).Value.MaintenanceOrderId;
        var scheduled = await w.As(w.Staff).ChangeStatusAsync(
            id, new(MaintenanceOrderStatus.Scheduled, at, null, null), default);

        created.Value.ScheduledAt.Should().Be(new DateTime(2026, 10, 20, 2, 0, 0));
        scheduled.Value.ScheduledAt.Should().Be(new DateTime(2026, 10, 20, 2, 0, 0));
    }
}
