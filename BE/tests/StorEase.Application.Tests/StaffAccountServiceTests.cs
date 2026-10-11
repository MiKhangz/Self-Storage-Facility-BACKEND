using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Staff;
using StorEase.Application.Features.Staff.Dtos;
using StorEase.Application.Features.Staff.Validators;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Tests;

public class StaffAccountServiceTests
{
    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(string value) => "hash:" + value;
        public bool Verify(string value, string hash) => hash == "hash:" + value;
    }

    private sealed class World
    {
        public TestDb Db { get; } = new();
        public Role StaffRole { get; } = new() { RoleName = Roles.FacilityStaff };
        public Role ManagerRole { get; } = new() { RoleName = Roles.FacilityManager };
        public Role AdminRole { get; } = new() { RoleName = Roles.SystemAdministrator };
        public Role CustomerRole { get; } = new() { RoleName = Roles.StorageCustomer };
        public Facility F1 { get; } = new() { Code = "F1", Name = "A Facility", Address = "a", District = "d" };
        public Facility F2 { get; } = new() { Code = "F2", Name = "B Facility", Address = "a", District = "d" };
        public User Admin { get; }
        public User Customer { get; }

        public World()
        {
            Admin = NewUser(AdminRole, "admin@x.vn");
            Customer = NewUser(CustomerRole, "cust@x.vn");
            Db.Db.AddRange(StaffRole, ManagerRole, AdminRole, CustomerRole, F1, F2, Admin, Customer);
            Db.Db.SaveChanges();
        }

        public StaffAccountService AsAdmin() =>
            new(Db.Uow, new FakeCurrentUser(Admin.UserId, Roles.SystemAdministrator), new FakeHasher());

        public static User NewUser(Role role, string email) => new()
        {
            Role = role, FullName = email, Email = email, PhoneNumber = "0900", PasswordHash = "x",
            Status = UserStatus.Active, CreatedAt = DateTime.UtcNow,
        };

        public static CreateStaffAccountRequest Create(string email = "new@x.vn", string role = Roles.FacilityStaff) =>
            new("New Person", email, "0901", "Secret123", role);
    }

    [Fact]
    public async Task Create_hashes_password_and_never_returns_it()
    {
        var w = new World();

        var r = await w.AsAdmin().CreateAsync(World.Create(), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Role.Should().Be(Roles.FacilityStaff);
        r.Value.Status.Should().Be(UserStatus.Active);
        r.Value.GetType().GetProperties().Select(p => p.Name).Should().NotContain(n => n.Contains("Password"));
        var stored = await w.Db.Db.Users.SingleAsync(u => u.Email == "new@x.vn");
        stored.PasswordHash.Should().Be("hash:Secret123");
    }

    [Fact]
    public async Task Create_with_duplicate_email_is_a_conflict_ignoring_case()
    {
        var w = new World();
        await w.AsAdmin().CreateAsync(World.Create("dup@x.vn"), default);

        var r = await w.AsAdmin().CreateAsync(World.Create("DUP@x.vn"), default);

        r.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Create_with_unseeded_role_is_not_found()
    {
        var w = new World();

        var r = await w.AsAdmin().CreateAsync(World.Create(role: Roles.BusinessOperationsManager), default);

        r.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Theory]
    [InlineData(Roles.StorageCustomer)]
    [InlineData("Hacker")]
    [InlineData("")]
    public void Validator_rejects_non_staff_roles(string role)
    {
        var result = new CreateStaffAccountRequestValidator().Validate(World.Create(role: role));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_rejects_short_password_and_bad_email()
    {
        var result = new CreateStaffAccountRequestValidator()
            .Validate(new CreateStaffAccountRequest("n", "not-an-email", "0901", "short", Roles.FacilityStaff));

        result.Errors.Select(e => e.PropertyName).Should().Contain(["Email", "Password"]);
    }

    [Fact]
    public async Task Update_changes_role_status_and_name()
    {
        var w = new World();
        var created = (await w.AsAdmin().CreateAsync(World.Create(), default)).Value!;

        var r = await w.AsAdmin().UpdateAsync(created.UserId,
            new UpdateStaffAccountRequest("Renamed", null, Roles.FacilityManager, UserStatus.Suspended), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.FullName.Should().Be("Renamed");
        r.Value.Role.Should().Be(Roles.FacilityManager);
        r.Value.Status.Should().Be(UserStatus.Suspended);
    }

    [Fact]
    public async Task Admin_cannot_lock_or_demote_themselves()
    {
        var w = new World();

        var lockSelf = await w.AsAdmin().UpdateAsync(w.Admin.UserId,
            new UpdateStaffAccountRequest(null, null, null, UserStatus.Inactive), default);
        var demoteSelf = await w.AsAdmin().UpdateAsync(w.Admin.UserId,
            new UpdateStaffAccountRequest(null, null, Roles.FacilityStaff, null), default);

        lockSelf.Error!.Type.Should().Be(ErrorType.Conflict);
        demoteSelf.Error!.Type.Should().Be(ErrorType.Conflict);
        (await w.Db.Db.Users.Include(u => u.Role).SingleAsync(u => u.UserId == w.Admin.UserId))
            .Should().Match<User>(u => u.Status == UserStatus.Active && u.Role.RoleName == Roles.SystemAdministrator);
    }

    [Fact]
    public async Task Customers_are_invisible_to_the_staff_api()
    {
        var w = new World();

        (await w.AsAdmin().GetAsync(w.Customer.UserId, default)).Error!.Type.Should().Be(ErrorType.NotFound);
        (await w.AsAdmin().UpdateAsync(w.Customer.UserId,
            new UpdateStaffAccountRequest("x", null, null, null), default)).Error!.Type.Should().Be(ErrorType.NotFound);
        (await w.AsAdmin().ListAsync(null, null, 1, 20, default)).Value.Items
            .Should().OnlyContain(u => u.Role != Roles.StorageCustomer);
    }

    [Fact]
    public async Task SetFacilities_replaces_the_whole_list()
    {
        var w = new World();
        var id = (await w.AsAdmin().CreateAsync(World.Create(), default)).Value!.UserId;
        await w.AsAdmin().SetFacilitiesAsync(id, new SetStaffFacilitiesRequest([w.F1.FacilityId]), default);

        var r = await w.AsAdmin().SetFacilitiesAsync(id, new SetStaffFacilitiesRequest([w.F2.FacilityId]), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Facilities.Select(f => f.FacilityId).Should().Equal(w.F2.FacilityId);
        (await w.Db.Db.StaffFacilities.CountAsync(s => s.UserId == id)).Should().Be(1);
    }

    [Fact]
    public async Task SetFacilities_keeps_existing_rows_ignores_duplicates_and_can_clear()
    {
        var w = new World();
        var id = (await w.AsAdmin().CreateAsync(World.Create(), default)).Value!.UserId;
        await w.AsAdmin().SetFacilitiesAsync(id, new SetStaffFacilitiesRequest([w.F1.FacilityId]), default);
        var keptRowId = (await w.Db.Db.StaffFacilities.SingleAsync(s => s.UserId == id)).StaffFacilityId;

        var both = await w.AsAdmin().SetFacilitiesAsync(id,
            new SetStaffFacilitiesRequest([w.F1.FacilityId, w.F2.FacilityId, w.F2.FacilityId]), default);
        var none = await w.AsAdmin().SetFacilitiesAsync(id, new SetStaffFacilitiesRequest([]), default);

        both.Value.Facilities.Should().HaveCount(2);
        w.Db.Db.StaffFacilities.Any(s => s.StaffFacilityId == keptRowId).Should().BeFalse("cleared by the last call");
        none.Value.Facilities.Should().BeEmpty();
    }

    [Fact]
    public async Task SetFacilities_with_unknown_facility_changes_nothing()
    {
        var w = new World();
        var id = (await w.AsAdmin().CreateAsync(World.Create(), default)).Value!.UserId;
        await w.AsAdmin().SetFacilitiesAsync(id, new SetStaffFacilitiesRequest([w.F1.FacilityId]), default);

        var r = await w.AsAdmin().SetFacilitiesAsync(id, new SetStaffFacilitiesRequest([w.F2.FacilityId, 999]), default);

        r.Error!.Type.Should().Be(ErrorType.NotFound);
        (await w.Db.Db.StaffFacilities.Where(s => s.UserId == id).Select(s => s.FacilityId).ToListAsync())
            .Should().Equal(w.F1.FacilityId);
    }

    [Fact]
    public async Task List_filters_by_role_and_status()
    {
        var w = new World();
        var a = (await w.AsAdmin().CreateAsync(World.Create("a@x.vn"), default)).Value!;
        await w.AsAdmin().CreateAsync(World.Create("b@x.vn", Roles.FacilityManager), default);
        await w.AsAdmin().UpdateAsync(a.UserId, new UpdateStaffAccountRequest(null, null, null, UserStatus.Inactive), default);

        var byRole = await w.AsAdmin().ListAsync(Roles.FacilityManager, null, 1, 20, default);
        var byStatus = await w.AsAdmin().ListAsync(null, UserStatus.Inactive, 1, 20, default);

        byRole.Value.Items.Select(i => i.Email).Should().Equal("b@x.vn");
        byStatus.Value.Items.Select(i => i.Email).Should().Equal("a@x.vn");
    }
}
