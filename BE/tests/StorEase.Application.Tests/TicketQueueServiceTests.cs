using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.SupportTickets;
using StorEase.Application.Features.SupportTickets.Dtos;
using StorEase.Application.Features.SupportTickets.Validators;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Tests;

public class TicketQueueServiceTests
{
    private sealed class World
    {
        public TestDb Db { get; } = new();
        public Role StaffRole { get; } = new() { RoleName = Roles.FacilityStaff };
        public Role ManagerRole { get; } = new() { RoleName = Roles.FacilityManager };
        public Role CustomerRole { get; } = new() { RoleName = Roles.StorageCustomer };
        public Facility F1 { get; } = new() { Code = "F1", Name = "F1", Address = "a", District = "d" };
        public Facility F2 { get; } = new() { Code = "F2", Name = "F2", Address = "a", District = "d" };
        public User Manager { get; set; } = null!;
        public User Staff { get; set; } = null!;
        public User Customer { get; set; } = null!;
        public User OtherCustomer { get; set; } = null!;
        public TicketCategory Category { get; } = new() { Name = "Lock", DefaultPriority = Priority.Normal };

        public World()
        {
            Manager = NewUser(ManagerRole, "m@x.vn");
            Staff = NewUser(StaffRole, "s@x.vn");
            Customer = NewUser(CustomerRole, "c@x.vn");
            OtherCustomer = NewUser(CustomerRole, "o@x.vn");
            Db.Db.AddRange(F1, F2, Category, Manager, Staff, Customer, OtherCustomer);
            Db.Db.StaffFacilities.AddRange(
                new StaffFacility { User = Manager, Facility = F1, AssignedDate = new DateOnly(2026, 10, 1) },
                new StaffFacility { User = Staff, Facility = F1, AssignedDate = new DateOnly(2026, 10, 1) });
            Db.Db.SaveChanges();
        }

        public SupportTicket AddTicket(Facility f, Priority p, DateTime createdAt, User? customer = null,
            SupportTicketStatus status = SupportTicketStatus.New)
        {
            var t = new SupportTicket
            {
                Customer = customer ?? Customer, Facility = f, Category = Category,
                TicketCode = "TK" + Guid.NewGuid().ToString("N")[..10], Subject = "s",
                Priority = p, Status = status, CreatedAt = createdAt,
            };
            Db.Db.SupportTickets.Add(t);
            Db.Db.SaveChanges();
            return t;
        }

        public TicketQueueService As(User u) => new(Db.Uow, new FakeCurrentUser(u.UserId, u.Role.RoleName));

        private static User NewUser(Role role, string email) => new()
        {
            Role = role, FullName = email, Email = email, PasswordHash = "x",
            Status = UserStatus.Active, CreatedAt = DateTime.UtcNow,
        };
    }

    [Fact]
    public async Task List_orders_by_priority_not_alphabetically_then_oldest_first()
    {
        var w = new World();
        var t0 = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
        var low = w.AddTicket(w.F1, Priority.Low, t0);
        var urgentLate = w.AddTicket(w.F1, Priority.Urgent, t0.AddHours(2));
        var normal = w.AddTicket(w.F1, Priority.Normal, t0);
        var high = w.AddTicket(w.F1, Priority.High, t0);
        var urgentEarly = w.AddTicket(w.F1, Priority.Urgent, t0.AddHours(1));

        var r = await w.As(w.Staff).ListAsync(null, null, null, false, 1, 20, default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Items.Select(i => i.TicketId).Should().Equal(
            urgentEarly.TicketId, urgentLate.TicketId, high.TicketId, normal.TicketId, low.TicketId);
    }

    [Fact]
    public async Task List_is_limited_to_staff_facilities_and_customer_own_tickets()
    {
        var w = new World();
        var mine = w.AddTicket(w.F1, Priority.Normal, DateTime.UtcNow);
        var otherFacility = w.AddTicket(w.F2, Priority.Normal, DateTime.UtcNow);
        var otherCustomer = w.AddTicket(w.F1, Priority.Normal, DateTime.UtcNow, w.OtherCustomer);

        var staff = await w.As(w.Staff).ListAsync(null, null, null, false, 1, 20, default);
        var customer = await w.As(w.Customer).ListAsync(null, null, null, false, 1, 20, default);
        var admin = await new TicketQueueService(w.Db.Uow, new FakeCurrentUser(99, Roles.SystemAdministrator))
            .ListAsync(null, null, null, false, 1, 20, default);

        staff.Value.Items.Select(i => i.TicketId).Should().BeEquivalentTo([mine.TicketId, otherCustomer.TicketId]);
        customer.Value.Items.Select(i => i.TicketId).Should().BeEquivalentTo([mine.TicketId, otherFacility.TicketId]);
        admin.Value.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task List_filters_by_status_priority_and_assigned_to_me()
    {
        var w = new World();
        var a = w.AddTicket(w.F1, Priority.High, DateTime.UtcNow);
        w.AddTicket(w.F1, Priority.Low, DateTime.UtcNow);
        var c = w.AddTicket(w.F1, Priority.High, DateTime.UtcNow, status: SupportTicketStatus.InProgress);
        c.AssignedTo = w.Staff.UserId;
        w.Db.Db.SaveChanges();
        var svc = w.As(w.Staff);

        (await svc.ListAsync(SupportTicketStatus.New, null, null, false, 1, 20, default))
            .Value.TotalCount.Should().Be(2);
        (await svc.ListAsync(null, Priority.High, null, false, 1, 20, default))
            .Value.Items.Select(i => i.TicketId).Should().BeEquivalentTo([a.TicketId, c.TicketId]);
        (await svc.ListAsync(null, null, null, true, 1, 20, default))
            .Value.Items.Should().ContainSingle().Which.TicketId.Should().Be(c.TicketId);
    }

    [Fact]
    public async Task Assign_sets_assignee_and_due_date_and_starts_a_new_ticket()
    {
        var w = new World();
        var t = w.AddTicket(w.F1, Priority.Normal, DateTime.UtcNow);
        var due = DateTime.UtcNow.Date.AddDays(2);

        var r = await w.As(w.Manager).AssignAsync(t.TicketId, new AssignTicketRequest(w.Staff.UserId, due), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.AssignedTo.Should().Be(w.Staff.UserId);
        r.Value.DueDate.Should().Be(due);
        r.Value.Status.Should().Be(SupportTicketStatus.InProgress);
    }

    [Fact]
    public async Task Assign_rejects_assignee_without_staff_facility_inactive_or_wrong_role()
    {
        var w = new World();
        var t = w.AddTicket(w.F1, Priority.Normal, DateTime.UtcNow);
        var elsewhere = new User
        {
            Role = w.StaffRole, FullName = "e", Email = "e@x.vn", PasswordHash = "x",
            Status = UserStatus.Active, CreatedAt = DateTime.UtcNow,
        };
        w.Db.Db.StaffFacilities.Add(new StaffFacility { User = elsewhere, Facility = w.F2, AssignedDate = new DateOnly(2026, 10, 1) });
        w.Db.Db.SaveChanges();
        var svc = w.As(w.Manager);

        (await svc.AssignAsync(t.TicketId, new AssignTicketRequest(elsewhere.UserId, null), default))
            .Error!.Type.Should().Be(ErrorType.Conflict);
        (await svc.AssignAsync(t.TicketId, new AssignTicketRequest(w.Customer.UserId, null), default))
            .Error!.Type.Should().Be(ErrorType.Conflict);

        // The failed calls above cleared the change tracker, so update by key rather than via the stale instance.
        w.Db.Db.Users.Where(u => u.UserId == w.Staff.UserId)
            .ExecuteUpdate(s => s.SetProperty(u => u.Status, UserStatus.Suspended));
        (await svc.AssignAsync(t.TicketId, new AssignTicketRequest(w.Staff.UserId, null), default))
            .Error!.Type.Should().Be(ErrorType.Conflict);
        (await svc.AssignAsync(t.TicketId, new AssignTicketRequest(12345, null), default))
            .Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Assign_a_ticket_of_another_facility_or_a_closed_one_fails()
    {
        var w = new World();
        var other = w.AddTicket(w.F2, Priority.Normal, DateTime.UtcNow);
        var closed = w.AddTicket(w.F1, Priority.Normal, DateTime.UtcNow, status: SupportTicketStatus.Closed);
        var svc = w.As(w.Manager);

        (await svc.AssignAsync(other.TicketId, new AssignTicketRequest(w.Staff.UserId, null), default))
            .Error!.Type.Should().Be(ErrorType.NotFound);
        (await svc.AssignAsync(closed.TicketId, new AssignTicketRequest(w.Staff.UserId, null), default))
            .Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Staff_walk_a_ticket_to_resolved_and_resolved_at_is_set()
    {
        var w = new World();
        var t = w.AddTicket(w.F1, Priority.Normal, DateTime.UtcNow);
        var svc = w.As(w.Staff);

        (await svc.ChangeStatusAsync(t.TicketId, new ChangeTicketStatusRequest(SupportTicketStatus.InProgress), default))
            .Value.Status.Should().Be(SupportTicketStatus.InProgress);
        (await svc.ChangeStatusAsync(t.TicketId, new ChangeTicketStatusRequest(SupportTicketStatus.WaitingForCustomer), default))
            .Value.Status.Should().Be(SupportTicketStatus.WaitingForCustomer);
        var resolved = await svc.ChangeStatusAsync(t.TicketId, new ChangeTicketStatusRequest(SupportTicketStatus.Resolved), default);

        resolved.Value.Status.Should().Be(SupportTicketStatus.Resolved);
        resolved.Value.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Invalid_staff_moves_and_same_status_are_conflicts()
    {
        var w = new World();
        var t = w.AddTicket(w.F1, Priority.Normal, DateTime.UtcNow);
        var svc = w.As(w.Staff);

        (await svc.ChangeStatusAsync(t.TicketId, new ChangeTicketStatusRequest(SupportTicketStatus.WaitingForCustomer), default))
            .Error!.Type.Should().Be(ErrorType.Conflict);
        (await svc.ChangeStatusAsync(t.TicketId, new ChangeTicketStatusRequest(SupportTicketStatus.New), default))
            .Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Closing_is_for_the_owner_customer_or_a_manager_and_closed_is_final()
    {
        var w = new World();
        var close = new ChangeTicketStatusRequest(SupportTicketStatus.Closed);
        var t1 = w.AddTicket(w.F1, Priority.Normal, DateTime.UtcNow);
        var t2 = w.AddTicket(w.F1, Priority.Normal, DateTime.UtcNow);

        (await w.As(w.Staff).ChangeStatusAsync(t1.TicketId, close, default)).Error!.Type.Should().Be(ErrorType.Forbidden);
        (await w.As(w.OtherCustomer).ChangeStatusAsync(t1.TicketId, close, default)).Error!.Type.Should().Be(ErrorType.NotFound);
        (await w.As(w.Customer).ChangeStatusAsync(t1.TicketId, close, default)).Value.Status.Should().Be(SupportTicketStatus.Closed);
        (await w.As(w.Manager).ChangeStatusAsync(t2.TicketId, close, default)).Value.Status.Should().Be(SupportTicketStatus.Closed);
        (await w.As(w.Manager).ChangeStatusAsync(t2.TicketId, new ChangeTicketStatusRequest(SupportTicketStatus.InProgress), default))
            .Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task A_customer_cannot_set_a_status_other_than_closed()
    {
        var w = new World();
        var t = w.AddTicket(w.F1, Priority.Normal, DateTime.UtcNow);

        (await w.As(w.Customer).ChangeStatusAsync(t.TicketId, new ChangeTicketStatusRequest(SupportTicketStatus.Resolved), default))
            .Error!.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public void Assign_validator_rejects_a_past_due_date()
    {
        var v = new AssignTicketRequestValidator();

        v.Validate(new AssignTicketRequest(1, DateTime.UtcNow.AddDays(-1))).IsValid.Should().BeFalse();
        v.Validate(new AssignTicketRequest(1, DateTime.UtcNow)).IsValid.Should().BeTrue();
        v.Validate(new AssignTicketRequest(1, null)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Assign_accepts_a_facility_manager_who_has_no_staff_facility_row()
    {
        var w = new World();
        var lead = new User
        {
            Role = w.ManagerRole, FullName = "lead", Email = "lead@x.vn", PasswordHash = "x",
            Status = UserStatus.Active, CreatedAt = DateTime.UtcNow,
        };
        w.Db.Db.Add(lead);
        w.F1.Manager = lead;
        await w.Db.Db.SaveChangesAsync();
        var t = w.AddTicket(w.F1, Priority.Normal, DateTime.UtcNow);

        var r = await w.As(w.Manager).AssignAsync(t.TicketId, new AssignTicketRequest(lead.UserId, null), default);

        r.Error.Should().BeNull();
        r.Value.AssignedTo.Should().Be(lead.UserId);
    }

    [Fact]
    public async Task Assign_reads_an_offsetless_due_date_as_vietnam_time()
    {
        var w = new World();
        var t = w.AddTicket(w.F1, Priority.Normal, DateTime.UtcNow);
        var due = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(2).AddHours(9), DateTimeKind.Unspecified);

        var r = await w.As(w.Manager).AssignAsync(t.TicketId, new AssignTicketRequest(w.Staff.UserId, due), default);

        r.Value.DueDate.Should().Be(due.AddHours(-7));
    }
}
