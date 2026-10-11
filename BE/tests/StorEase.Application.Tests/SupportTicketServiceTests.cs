using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.SupportTickets;
using StorEase.Application.Features.SupportTickets.Dtos;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Tests;

public class SupportTicketServiceTests
{
    private sealed class World
    {
        public required TestDb Db { get; init; }
        public required User Customer { get; init; }
        public required User Other { get; init; }
        public required User Staff { get; init; }
        public required User OutsideStaff { get; init; }
        public required Facility Facility { get; init; }
        public required StorageUnit Unit { get; init; }
        public required StorageUnit OthersUnit { get; init; }
        public required TicketCategory Category { get; init; }

        public SupportTicketService As(User u) => new(Db.Uow, new FakeCurrentUser(u.UserId, u.Role.RoleName));
    }

    private static async Task<World> SeedAsync(ContractStatus contractStatus = ContractStatus.Active)
    {
        var db = new TestDb();
        var customerRole = new Role { RoleName = Roles.StorageCustomer };
        var staffRole = new Role { RoleName = Roles.FacilityStaff };
        var customer = NewUser(customerRole, "c@x.vn");
        var other = NewUser(customerRole, "o@x.vn");
        var staff = NewUser(staffRole, "s@x.vn");
        var outside = NewUser(staffRole, "s2@x.vn");
        var facility = NewFacility("F1");
        var elsewhere = NewFacility("F2");
        var floor = new Floor { Facility = facility, FloorNumber = 1 };
        var type = new UnitType { Code = "S", Name = "Small" };
        var unit = new StorageUnit { Facility = facility, Floor = floor, UnitType = type, UnitCode = "A1", Status = StorageUnitStatus.Occupied };
        var othersUnit = new StorageUnit { Facility = facility, Floor = floor, UnitType = type, UnitCode = "A2", Status = StorageUnitStatus.Occupied };
        var category = new TicketCategory { Name = "Lock", DefaultPriority = Priority.High };
        db.Db.AddRange(customer, other, staff, outside, elsewhere, unit, othersUnit, category);
        db.Db.StaffFacilities.AddRange(
            new StaffFacility { User = staff, Facility = facility, AssignedDate = new DateOnly(2026, 10, 1) },
            new StaffFacility { User = outside, Facility = elsewhere, AssignedDate = new DateOnly(2026, 10, 1) });
        await db.Db.SaveChangesAsync();

        db.Db.Add(NewContract(customer, unit, contractStatus, "CT1"));
        db.Db.Add(NewContract(other, othersUnit, ContractStatus.Active, "CT2"));
        await db.Db.SaveChangesAsync();

        return new World
        {
            Db = db, Customer = customer, Other = other, Staff = staff, OutsideStaff = outside,
            Facility = facility, Unit = unit, OthersUnit = othersUnit, Category = category,
        };
    }

    private static CreateTicketRequest Req(World w, int? unitId = null, int? facilityId = null, Priority? priority = null) =>
        new(w.Category.CategoryId, "Lock jammed", "Cannot open the door.", unitId, facilityId, priority);

    [Fact]
    public async Task Create_on_own_unit_takes_facility_from_unit_and_priority_from_category()
    {
        var w = await SeedAsync();
        using var _ = w.Db;

        var result = await w.As(w.Customer).CreateAsync(Req(w, unitId: w.Unit.StorageUnitId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.FacilityId.Should().Be(w.Facility.FacilityId);
        result.Value.Priority.Should().Be(Priority.High);
        result.Value.Status.Should().Be(SupportTicketStatus.New);
        result.Value.TicketCode.Should().MatchRegex(@"^TK\d{12}$");
        var messages = await w.Db.Db.TicketMessages.ToListAsync();
        messages.Should().ContainSingle().Which.Body.Should().Be("Cannot open the door.");
    }

    [Fact]
    public async Task Create_with_explicit_priority_overrides_category_default()
    {
        var w = await SeedAsync();
        using var _ = w.Db;

        var result = await w.As(w.Customer).CreateAsync(Req(w, facilityId: w.Facility.FacilityId, priority: Priority.Low), default);

        result.Value.Priority.Should().Be(Priority.Low);
    }

    [Fact]
    public async Task Create_on_someone_elses_unit_is_404()
    {
        var w = await SeedAsync();
        using var _ = w.Db;

        var result = await w.As(w.Customer).CreateAsync(Req(w, unitId: w.OthersUnit.StorageUnitId), default);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Create_on_unit_whose_contract_is_closed_is_404()
    {
        var w = await SeedAsync(ContractStatus.Closed);
        using var _ = w.Db;

        var result = await w.As(w.Customer).CreateAsync(Req(w, unitId: w.Unit.StorageUnitId), default);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Create_with_unknown_category_is_404()
    {
        var w = await SeedAsync();
        using var _ = w.Db;

        var result = await w.As(w.Customer).CreateAsync(Req(w, facilityId: w.Facility.FacilityId) with { CategoryId = 999 }, default);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Get_hides_other_customers_and_out_of_scope_staff_but_shows_in_scope_staff()
    {
        var w = await SeedAsync();
        using var _ = w.Db;
        var id = (await w.As(w.Customer).CreateAsync(Req(w, unitId: w.Unit.StorageUnitId), default)).Value.TicketId;

        (await w.As(w.Other).GetAsync(id, default)).Error!.Type.Should().Be(ErrorType.NotFound);
        (await w.As(w.OutsideStaff).GetAsync(id, default)).Error!.Type.Should().Be(ErrorType.NotFound);
        (await w.As(w.Staff).GetAsync(id, default)).IsSuccess.Should().BeTrue();
        (await w.As(w.Customer).GetAsync(id, default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Customer_never_sees_internal_notes_but_staff_do()
    {
        var w = await SeedAsync();
        using var _ = w.Db;
        var id = (await w.As(w.Customer).CreateAsync(Req(w, unitId: w.Unit.StorageUnitId), default)).Value.TicketId;
        await w.As(w.Staff).PostMessageAsync(id, new("secret", true, null), default);
        await w.As(w.Staff).PostMessageAsync(id, new("hello", false, null), default);

        var forCustomer = (await w.As(w.Customer).ListMessagesAsync(id, default)).Value!;
        var forStaff = (await w.As(w.Staff).ListMessagesAsync(id, default)).Value!;

        forCustomer.Select(m => m.Body).Should().Equal("Cannot open the door.", "hello");
        forStaff.Select(m => m.Body).Should().Equal("Cannot open the door.", "secret", "hello");
    }

    [Fact]
    public async Task Customer_cannot_post_internal_note_or_set_status()
    {
        var w = await SeedAsync();
        using var _ = w.Db;
        var id = (await w.As(w.Customer).CreateAsync(Req(w, unitId: w.Unit.StorageUnitId), default)).Value.TicketId;

        (await w.As(w.Customer).PostMessageAsync(id, new("x", true, null), default)).Error!.Type.Should().Be(ErrorType.Forbidden);
        (await w.As(w.Customer).PostMessageAsync(id, new("x", false, SupportTicketStatus.InProgress), default))
            .Error!.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Status_follows_the_conversation()
    {
        var w = await SeedAsync();
        using var _ = w.Db;
        var id = (await w.As(w.Customer).CreateAsync(Req(w, unitId: w.Unit.StorageUnitId), default)).Value.TicketId;

        // An internal note alone does not start work.
        await w.As(w.Staff).PostMessageAsync(id, new("note", true, null), default);
        (await StatusAsync(w, id)).Should().Be(SupportTicketStatus.New);

        await w.As(w.Staff).PostMessageAsync(id, new("on it", false, null), default);
        (await StatusAsync(w, id)).Should().Be(SupportTicketStatus.InProgress);

        await w.As(w.Staff).PostMessageAsync(id, new("what colour?", false, SupportTicketStatus.WaitingForCustomer), default);
        (await StatusAsync(w, id)).Should().Be(SupportTicketStatus.WaitingForCustomer);

        await w.As(w.Customer).PostMessageAsync(id, new("blue", false, null), default);
        (await StatusAsync(w, id)).Should().Be(SupportTicketStatus.InProgress);
    }

    [Fact]
    public async Task Customer_reply_reopens_a_resolved_ticket()
    {
        var w = await SeedAsync();
        using var _ = w.Db;
        var id = (await w.As(w.Customer).CreateAsync(Req(w, unitId: w.Unit.StorageUnitId), default)).Value.TicketId;
        var ticket = await w.Db.Db.SupportTickets.SingleAsync();
        ticket.Status = SupportTicketStatus.Resolved;
        ticket.ResolvedAt = DateTime.UtcNow;
        await w.Db.Db.SaveChangesAsync();

        await w.As(w.Customer).PostMessageAsync(id, new("still broken", false, null), default);

        var after = await w.Db.Db.SupportTickets.AsNoTracking().SingleAsync();
        after.Status.Should().Be(SupportTicketStatus.InProgress);
        after.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public async Task Closed_ticket_rejects_messages_with_409()
    {
        var w = await SeedAsync();
        using var _ = w.Db;
        var id = (await w.As(w.Customer).CreateAsync(Req(w, unitId: w.Unit.StorageUnitId), default)).Value.TicketId;
        (await w.Db.Db.SupportTickets.SingleAsync()).Status = SupportTicketStatus.Closed;
        await w.Db.Db.SaveChangesAsync();

        var result = await w.As(w.Customer).PostMessageAsync(id, new("hi", false, null), default);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Post_on_someone_elses_ticket_is_404()
    {
        var w = await SeedAsync();
        using var _ = w.Db;
        var id = (await w.As(w.Customer).CreateAsync(Req(w, unitId: w.Unit.StorageUnitId), default)).Value.TicketId;

        var result = await w.As(w.Other).PostMessageAsync(id, new("hi", false, null), default);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    private static async Task<SupportTicketStatus> StatusAsync(World w, int id) =>
        (await w.Db.Db.SupportTickets.AsNoTracking().SingleAsync(t => t.TicketId == id)).Status;

    private static User NewUser(Role role, string email) => new()
    {
        Role = role, FullName = "Test", Email = email, PasswordHash = "x", CreatedAt = DateTime.UtcNow,
    };

    private static Facility NewFacility(string code) => new()
    {
        Code = code, Name = code, Address = "addr", District = "d", Status = FacilityStatus.Active,
    };

    private static Contract NewContract(User customer, StorageUnit unit, ContractStatus status, string number) => new()
    {
        Reservation = new Reservation
        {
            Customer = customer, FacilityId = unit.FacilityId, UnitTypeId = unit.UnitTypeId, StorageUnit = unit,
            ReservationCode = "R" + number, MoveInDate = new DateOnly(2026, 10, 1), RentalMonths = 3,
            Status = ReservationStatus.CheckedIn, CreatedAt = DateTime.UtcNow,
        },
        Customer = customer, StorageUnit = unit, ContractNumber = number,
        StartDate = new DateOnly(2026, 10, 1), EndDate = new DateOnly(2027, 1, 1), Status = status,
    };

    [Fact]
    public async Task Staff_status_in_a_message_follows_the_transition_table()
    {
        var w = await SeedAsync();
        using var _ = w.Db;
        var id = (await w.As(w.Customer).CreateAsync(Req(w, unitId: w.Unit.StorageUnitId), default)).Value.TicketId;

        // New cannot jump to WaitingForCustomer, and plain staff cannot close.
        var skipped = await w.As(w.Staff).PostMessageAsync(id, new("hi", false, SupportTicketStatus.WaitingForCustomer), default);
        var closed = await w.As(w.Staff).PostMessageAsync(id, new("bye", false, SupportTicketStatus.Closed), default);

        skipped.Error!.Type.Should().Be(ErrorType.Conflict);
        closed.Error!.Type.Should().Be(ErrorType.Forbidden);
        (await StatusAsync(w, id)).Should().Be(SupportTicketStatus.New);
        (await w.Db.Db.TicketMessages.CountAsync(m => m.TicketId == id)).Should().Be(1, "refused replies are not stored");
    }

    [Fact]
    public async Task Staff_cannot_reopen_a_resolved_ticket_through_a_message_but_may_resolve_with_a_timestamp()
    {
        var w = await SeedAsync();
        using var _ = w.Db;
        var id = (await w.As(w.Customer).CreateAsync(Req(w, unitId: w.Unit.StorageUnitId), default)).Value.TicketId;

        var resolved = await w.As(w.Staff).PostMessageAsync(id, new("fixed", false, SupportTicketStatus.Resolved), default);
        var reopened = await w.As(w.Staff).PostMessageAsync(id, new("oops", false, SupportTicketStatus.InProgress), default);

        resolved.IsSuccess.Should().BeTrue();
        (await w.Db.Db.SupportTickets.AsNoTracking().SingleAsync()).ResolvedAt.Should().NotBeNull();
        reopened.Error!.Type.Should().Be(ErrorType.Conflict);
        (await StatusAsync(w, id)).Should().Be(SupportTicketStatus.Resolved);
    }

    private sealed class FixedCodeService(StorEase.Application.Interfaces.IUnitOfWork uow, StorEase.Application.Interfaces.ICurrentUser user)
        : SupportTicketService(uow, user)
    {
        protected override string NewTicketCode() => "TKCLASH";
    }

    [Fact]
    public async Task Clashing_ticket_code_is_409_not_500()
    {
        var w = await SeedAsync();
        using var _ = w.Db;
        var svc = new FixedCodeService(w.Db.Uow, new FakeCurrentUser(w.Customer.UserId, Roles.StorageCustomer));

        var first = await svc.CreateAsync(Req(w, unitId: w.Unit.StorageUnitId), default);
        var second = await svc.CreateAsync(Req(w, unitId: w.Unit.StorageUnitId), default);

        first.IsSuccess.Should().BeTrue();
        second.Error!.Type.Should().Be(ErrorType.Conflict);
    }
}
