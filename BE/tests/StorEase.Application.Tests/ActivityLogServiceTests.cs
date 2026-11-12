using FluentAssertions;
using StorEase.Application.Common;
using StorEase.Application.Features.ActivityLogs;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Tests;

public class ActivityLogServiceTests
{
    private static (TestDb Db, int Staff, int Customer) WithLogs()
    {
        var t = new TestDb();
        User Person(string role, string name, string email) => new()
        {
            Role = new Role { RoleName = role }, FullName = name, Email = email, PhoneNumber = "0900000000",
            PasswordHash = "x", Status = UserStatus.Active, CreatedAt = DateTime.UtcNow,
        };
        var staff = Person(Roles.FacilityStaff, "Lan Staff", "lan@x.vn");
        var customer = Person(Roles.StorageCustomer, "Minh Customer", "minh@x.vn");
        t.Db.AddRange(staff, customer);
        t.Db.SaveChanges();
        var service = new ActivityLogService(t.Uow);
        foreach (var (user, action) in new[] { (staff, ActivityLogService.SignIn), (staff, "POST Handovers.Create"), (customer, ActivityLogService.SignIn), (customer, "POST Reservations.Create") })
            service.RecordAsync(new ActivityEntry(user.UserId, null, action, null, null, null, true), default).GetAwaiter().GetResult();
        return (t, staff.UserId, customer.UserId);
    }

    private static async Task<IReadOnlyList<ActivityLogDto>> List(TestDb t, int? userId = null, string? role = null, string? search = null, bool signIns = true) =>
        (await new ActivityLogService(t.Uow).ListAsync(userId, role, search, null, signIns, null, null, 1, 50, default)).Value!.Items;

    [Fact]
    public async Task Every_row_carries_the_users_role_and_customers_are_included()
    {
        var (t, _, customer) = WithLogs();
        using var _db = t;

        var rows = await List(t);

        rows.Should().HaveCount(4);
        rows.Where(r => r.UserId == customer).Should().OnlyContain(r => r.UserRole == Roles.StorageCustomer);
    }

    [Fact]
    public async Task Filters_narrow_by_role_user_name_or_email_and_can_leave_out_sign_ins()
    {
        var (t, staff, customer) = WithLogs();
        using var _db = t;

        (await List(t, role: Roles.StorageCustomer)).Should().OnlyContain(r => r.UserId == customer).And.HaveCount(2);
        (await List(t, userId: staff)).Should().OnlyContain(r => r.UserId == staff).And.HaveCount(2);
        (await List(t, search: "Minh")).Should().OnlyContain(r => r.UserId == customer);
        (await List(t, search: "lan@x")).Should().OnlyContain(r => r.UserId == staff);
        (await List(t, signIns: false)).Select(r => r.Action).Should().BeEquivalentTo("POST Handovers.Create", "POST Reservations.Create");
    }

    [Fact]
    public async Task Areas_narrow_the_log_to_those_parts_of_the_system()
    {
        var (t, staff, _) = WithLogs();
        using var _db = t;
        var service = new ActivityLogService(t.Uow);
        await service.RecordAsync(new ActivityEntry(staff, null, "POST Reservations.Create", "Reservations", "9", null, true), default);
        await service.RecordAsync(new ActivityEntry(staff, null, "POST Returns.Create", "Returns", "4", null, true), default);

        var rows = (await service.ListAsync(null, null, null, null, true, null, null, 1, 50, default, "Reservations, Returns")).Value!.Items;

        rows.Select(r => r.EntityName).Should().BeEquivalentTo("Reservations", "Returns");
    }

    [Fact]
    public async Task Leaving_out_sign_ins_also_hides_the_older_duplicate_sign_in_rows()
    {
        var (t, staff, _) = WithLogs();
        using var _db = t;
        await new ActivityLogService(t.Uow).RecordAsync(new ActivityEntry(staff, null, "POST Auth.Login", "Auth", null, null, true), default);

        (await List(t, signIns: false)).Should().NotContain(r => r.Action == "POST Auth.Login");
    }
}
