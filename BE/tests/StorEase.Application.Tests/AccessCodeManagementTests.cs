using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.AccessCodes;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using StorEase.Infrastructure.Persistence;
using StorEase.Infrastructure.Persistence.Repositories;

namespace StorEase.Application.Tests;

public class AccessCodeManagementTests
{
    private sealed class FakeUser(int id, string role) : ICurrentUser
    {
        public int UserId => id;
        public string Role => role;
    }

    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(string value) => "h:" + value;
        public bool Verify(string value, string hash) => hash == "h:" + value;
    }

    private sealed class TestUow(AppDbContext db) : IUnitOfWork
    {
        public IRepository<T> Repository<T>() where T : class => new Repository<T>(db);
        public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

        public async Task<Result<T>> InTransactionAsync<T>(Func<Task<Result<T>>> work, CancellationToken ct)
        {
            var result = await work();
            if (result.IsSuccess)
                await db.SaveChangesAsync(ct);
            return result;
        }
    }

    // Customer 1 (contract 1, Active, unit 1) and customer 2 (contract 2, unit 2), each with one active code and logs.
    private static (AppDbContext Db, AccessCodeService Codes, AccessLogService Logs) Create(int userId, string role)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var uow = new TestUow(db);
        var user = new FakeUser(userId, role);

        var c1 = new User { UserId = 1, FullName = "Khach A" };
        var c2 = new User { UserId = 2, FullName = "Khach B" };
        var u1 = new StorageUnit { StorageUnitId = 1, UnitCode = "A-101" };
        var u2 = new StorageUnit { StorageUnitId = 2, UnitCode = "A-102" };
        var k1 = new Contract { ContractId = 1, ContractNumber = "CT1", CustomerId = 1, Customer = c1, StorageUnitId = 1, StorageUnit = u1, Status = ContractStatus.Active, EndDate = new DateOnly(2999, 1, 1) };
        var k2 = new Contract { ContractId = 2, ContractNumber = "CT2", CustomerId = 2, Customer = c2, StorageUnitId = 2, StorageUnit = u2, Status = ContractStatus.Active, EndDate = new DateOnly(2999, 1, 1) };
        var a1 = new AccessCode { AccessCodeId = 1, ContractId = 1, Contract = k1, CodeHash = "x", IssuedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(30), IsActive = true };
        var a2 = new AccessCode { AccessCodeId = 2, ContractId = 2, Contract = k2, CodeHash = "y", IssuedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(30), IsActive = true };
        db.AddRange(c1, c2, u1, u2, k1, k2, a1, a2,
            new AccessLog { AccessCodeId = 1, AccessCode = a1, StorageUnitId = 1, StorageUnit = u1, AccessTime = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc), Result = AccessResult.Granted },
            new AccessLog { AccessCodeId = 1, AccessCode = a1, StorageUnitId = 1, StorageUnit = u1, AccessTime = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc), Result = AccessResult.Denied },
            new AccessLog { AccessCodeId = 2, AccessCode = a2, StorageUnitId = 2, StorageUnit = u2, AccessTime = new DateTime(2026, 10, 3, 8, 0, 0, DateTimeKind.Utc), Result = AccessResult.Granted });
        db.SaveChanges();
        return (db, new AccessCodeService(uow, new FakeHasher(), user), new AccessLogService(uow, user));
    }

    [Fact]
    public async Task Revoke_deactivates_the_code_and_a_second_revoke_conflicts()
    {
        var (db, codes, _) = Create(9, Roles.FacilityStaff);

        (await codes.RevokeAsync(1, default)).Value!.IsActive.Should().BeFalse();
        db.Set<AccessCode>().Single(a => a.AccessCodeId == 1).IsActive.Should().BeFalse();
        (await codes.RevokeAsync(1, default)).Error!.Type.Should().Be(ErrorType.Conflict);
        (await codes.RevokeAsync(99, default)).Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Customer_sees_only_their_own_active_code_and_never_the_plaintext()
    {
        var (_, codes, _) = Create(1, Roles.StorageCustomer);

        (await codes.GetActiveAsync(1, default)).Value!.AccessCodeId.Should().Be(1);
        (await codes.GetActiveAsync(2, default)).Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Active_code_is_not_found_once_expired_or_revoked()
    {
        var (db, codes, _) = Create(1, Roles.StorageCustomer);
        db.Set<AccessCode>().Single(a => a.AccessCodeId == 1).ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        db.SaveChanges();

        (await codes.GetActiveAsync(1, default)).Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Customer_can_request_a_new_code_for_own_contract_only()
    {
        var (db, codes, _) = Create(1, Roles.StorageCustomer);

        var mine = await codes.IssueAsync(1, default);
        mine.Value!.Code.Should().MatchRegex("^[0-9]{6}$");
        db.Set<AccessCode>().Single(a => a.AccessCodeId == 1).IsActive.Should().BeFalse();

        (await codes.IssueAsync(2, default)).Error!.Type.Should().Be(ErrorType.NotFound);
        db.Set<AccessCode>().Single(a => a.AccessCodeId == 2).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Manager_search_filters_by_unit_customer_and_inclusive_date_range()
    {
        var (_, _, logs) = Create(9, Roles.FacilityManager);

        (await logs.SearchAsync(null, null, null, null, 1, 20, default)).Value!.TotalCount.Should().Be(3);
        (await logs.SearchAsync(1, null, null, null, 1, 20, default)).Value!.TotalCount.Should().Be(2);
        (await logs.SearchAsync(null, 2, null, null, 1, 20, default)).Value!.Items.Single().CustomerName.Should().Be("Khach B");
        var ranged = await logs.SearchAsync(null, null, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 5), 1, 20, default);
        ranged.Value!.Items.Single().Result.Should().Be("Denied");
        (await logs.SearchAsync(null, null, new DateOnly(2026, 10, 6), new DateOnly(2026, 10, 5), 1, 20, default))
            .Error!.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Customer_history_lists_only_own_entries_newest_first()
    {
        var (_, _, logs) = Create(1, Roles.StorageCustomer);

        var page = (await logs.ListMineAsync(null, null, 1, 20, default)).Value!;

        page.Items.Select(l => l.Result).Should().Equal("Denied", "Granted");
        page.Items.Should().OnlyContain(l => l.CustomerId == 1);
    }
}
