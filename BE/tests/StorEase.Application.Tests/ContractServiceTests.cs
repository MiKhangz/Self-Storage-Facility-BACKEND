using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.AccessCodes;
using StorEase.Application.Features.Contracts;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using StorEase.Infrastructure.Persistence;
using StorEase.Infrastructure.Persistence.Repositories;

namespace StorEase.Application.Tests;

public class ContractServiceTests
{
    private sealed class FakeUser(int id, string role) : ICurrentUser
    {
        public int UserId => id;
        public string Role => role;
    }

    // InMemory has no transactions, so run the work directly and save on success like the real unit of work.
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

    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(string value) => "h:" + value;
        public bool Verify(string value, string hash) => hash == "h:" + value;
    }

    private static (ContractService Service, AppDbContext Db) Create(int userId, string role)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var customer = new User { UserId = 1, FullName = "Khach A" };
        var other = new User { UserId = 2, FullName = "Khach B" };
        var unit = new StorageUnit { StorageUnitId = 1, FacilityId = 1, UnitCode = "A-101" };
        // Staff user 9 works at facility 1; user 10 works nowhere.
        db.AddRange(customer, other, unit, new StaffFacility { UserId = 9, FacilityId = 1 },
            new Contract { ContractId = 1, ContractNumber = "CT1", CustomerId = 1, Customer = customer, StorageUnitId = 1, StorageUnit = unit, Status = ContractStatus.Draft },
            new Contract { ContractId = 2, ContractNumber = "CT2", CustomerId = 1, Customer = customer, StorageUnitId = 1, StorageUnit = unit, Status = ContractStatus.Active, SignedAt = DateTime.UtcNow },
            new Contract { ContractId = 3, ContractNumber = "CT3", CustomerId = 2, Customer = other, StorageUnitId = 1, StorageUnit = unit, Status = ContractStatus.Draft });
        db.SaveChanges();
        return (new ContractService(new TestUow(db), new FakeUser(userId, role),
            new AccessCodeService(new TestUow(db), new FakeHasher(), new FakeUser(userId, role))), db);
    }

    [Fact]
    public async Task Sign_sets_SignedAt_and_keeps_Draft()
    {
        var (service, db) = Create(1, Roles.StorageCustomer);

        var result = await service.SignAsync(1, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.SignedAt.Should().NotBeNull();
        result.Value.Status.Should().Be(ContractStatus.Draft);
        db.Contracts.Find(1)!.SignedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Sign_twice_conflicts()
    {
        var (service, _) = Create(1, Roles.StorageCustomer);
        await service.SignAsync(1, default);

        var result = await service.SignAsync(1, default);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Sign_non_draft_conflicts()
    {
        var (service, _) = Create(1, Roles.StorageCustomer);

        (await service.SignAsync(2, default)).Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Customer_cannot_sign_or_read_someone_elses_contract()
    {
        var (service, db) = Create(1, Roles.StorageCustomer);

        (await service.SignAsync(3, default)).Error!.Type.Should().Be(ErrorType.NotFound);
        (await service.GetAsync(3, default)).Error!.Type.Should().Be(ErrorType.NotFound);
        db.Contracts.Find(3)!.SignedAt.Should().BeNull();
    }

    [Fact]
    public async Task Customer_list_shows_only_own_and_staff_list_shows_all()
    {
        var (customerService, _) = Create(1, Roles.StorageCustomer);
        var (staffService, _) = Create(9, Roles.FacilityStaff);

        (await customerService.ListAsync(null, 1, 20, default)).Value!.TotalCount.Should().Be(2);
        (await staffService.ListAsync(null, 1, 20, default)).Value!.TotalCount.Should().Be(3);
        (await staffService.ListAsync(ContractStatus.Active, 1, 20, default)).Value!.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Staff_read_any_contract_at_their_facility_and_none_elsewhere()
    {
        var (service, _) = Create(9, Roles.FacilityStaff);
        var (outsider, _) = Create(10, Roles.FacilityStaff);
        var (admin, _) = Create(11, Roles.SystemAdministrator);

        var result = await service.GetAsync(3, default);

        result.Value!.CustomerName.Should().Be("Khach B");
        result.Value.UnitCode.Should().Be("A-101");
        (await outsider.GetAsync(3, default)).Error!.Type.Should().Be(ErrorType.NotFound);
        (await outsider.ListAsync(null, 1, 20, default)).Value!.TotalCount.Should().Be(0);
        (await admin.ListAsync(null, 1, 20, default)).Value!.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Staff_record_a_paper_signature_only_at_their_facility()
    {
        var (staff, db) = Create(9, Roles.FacilityStaff);
        var (outsider, _) = Create(10, Roles.FacilityStaff);

        var signed = await staff.SignAsync(3, default);
        var elsewhere = await outsider.SignAsync(1, default);

        signed.Value!.SignedAt.Should().NotBeNull();
        signed.Value.Status.Should().Be(ContractStatus.Draft, "signing never activates; check-in does");
        elsewhere.Error!.Type.Should().Be(ErrorType.NotFound);
    }
}
