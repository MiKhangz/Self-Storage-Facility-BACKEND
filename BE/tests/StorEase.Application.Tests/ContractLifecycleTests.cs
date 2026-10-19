using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.AccessCodes;
using StorEase.Application.Features.Contracts;
using StorEase.Application.Features.CheckIns;
using StorEase.Application.Features.CheckIns.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using StorEase.Infrastructure.Persistence;
using StorEase.Infrastructure.Persistence.Repositories;

namespace StorEase.Application.Tests;

public class ContractLifecycleTests
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

    // InMemory has no transactions: run the work directly and save only on success, like the real unit of work.
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

    private sealed record Fixture(AppDbContext Db, ContractService Contracts, AccessCodeService Codes, CheckInService CheckIns);

    private static Fixture Create(string role = Roles.FacilityStaff)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var uow = new TestUow(db);
        var user = new FakeUser(9, role);
        var codes = new AccessCodeService(uow, new FakeHasher(), user);
        var contracts = new ContractService(uow, user, codes);
        return new Fixture(db, contracts, codes, new CheckInService(uow, user, contracts));
    }

    // One confirmed reservation, its Reserved unit and a Draft contract.
    private static void Seed(AppDbContext db, bool signed = true)
    {
        var customer = new User { UserId = 1, FullName = "Khach A" };
        var unit = new StorageUnit { StorageUnitId = 1, FacilityId = 1, UnitCode = "A-101", Status = StorageUnitStatus.Reserved };
        var reservation = new Reservation { ReservationId = 1, FacilityId = 1, CustomerId = 1, StorageUnitId = 1, Status = ReservationStatus.Confirmed };
        // The acting staff member (user 9) works at facility 1.
        db.Add(new StaffFacility { UserId = 9, FacilityId = 1 });
        db.AddRange(customer, unit, reservation, new Contract
        {
            ContractId = 1, ContractNumber = "CT1", ReservationId = 1, CustomerId = 1, Customer = customer,
            StorageUnitId = 1, StorageUnit = unit, StartDate = new DateOnly(2026, 10, 13), EndDate = new DateOnly(2027, 1, 13),
            SignedAt = signed ? DateTime.UtcNow : null, Status = ContractStatus.Draft,
        });
        db.SaveChanges();
    }

    private static CreateCheckInRequest CheckIn() => new(1, 1, "LK1", "CARD1", null);

    [Fact]
    public async Task CheckIn_activates_contract_occupies_unit_and_issues_a_hashed_code()
    {
        var f = Create();
        Seed(f.Db);

        var result = await f.CheckIns.CreateAsync(CheckIn(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.AccessCode.Should().MatchRegex("^[0-9]{6}$");
        f.Db.Set<Contract>().Single().Status.Should().Be(ContractStatus.Active);
        f.Db.Set<StorageUnit>().Single().Status.Should().Be(StorageUnitStatus.Occupied);
        var stored = f.Db.Set<AccessCode>().Single();
        stored.IsActive.Should().BeTrue();
        stored.CodeHash.Should().Be("h:" + result.Value.AccessCode);
        stored.ExpiresAt.Should().Be(new DateTime(2027, 1, 14, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task CheckIn_refuses_an_unsigned_contract_and_changes_nothing()
    {
        var f = Create();
        Seed(f.Db, signed: false);

        var result = await f.CheckIns.CreateAsync(CheckIn(), default);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
        f.Db.Set<Contract>().Single().Status.Should().Be(ContractStatus.Draft);
        f.Db.Set<AccessCode>().Should().BeEmpty();
    }

    [Fact]
    public async Task Activate_refuses_when_another_contract_holds_the_unit()
    {
        var f = Create();
        Seed(f.Db);
        f.Db.Add(new Contract { ContractId = 2, ContractNumber = "CT2", ReservationId = 1, CustomerId = 1, StorageUnitId = 1, Status = ContractStatus.Active });
        f.Db.SaveChanges();

        var result = await f.Contracts.ActivateAsync(1, default);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
        f.Db.Set<Contract>().Single(c => c.ContractId == 1).Status.Should().Be(ContractStatus.Draft);
    }

    [Fact]
    public async Task Activate_ignores_terminated_contracts_on_the_same_unit()
    {
        var f = Create();
        Seed(f.Db);
        f.Db.Add(new Contract { ContractId = 2, ContractNumber = "CT2", ReservationId = 1, CustomerId = 1, StorageUnitId = 1, Status = ContractStatus.Terminated });
        f.Db.SaveChanges();

        (await f.Contracts.ActivateAsync(1, default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Issue_needs_an_active_contract_and_revokes_the_previous_code()
    {
        var f = Create();
        Seed(f.Db);
        (await f.Codes.IssueAsync(1, default)).Error!.Type.Should().Be(ErrorType.Conflict); // still Draft

        await f.CheckIns.CreateAsync(CheckIn(), default);
        var second = await f.Codes.IssueAsync(1, default);

        second.IsSuccess.Should().BeTrue();
        second.Value!.AccessCodeId.Should().BeGreaterThan(0);
        f.Db.Set<AccessCode>().Count(a => a.IsActive).Should().Be(1);
        f.Db.Set<AccessCode>().Single(a => a.IsActive).AccessCodeId.Should().Be(second.Value.AccessCodeId);
    }

    [Fact]
    public async Task Terminate_frees_the_unit_and_revokes_codes()
    {
        var f = Create(Roles.FacilityManager);
        Seed(f.Db);
        await f.CheckIns.CreateAsync(CheckIn(), default);

        var result = await f.Contracts.TerminateAsync(1, default);

        result.Value!.Status.Should().Be(ContractStatus.Terminated);
        f.Db.Set<StorageUnit>().Single().Status.Should().Be(StorageUnitStatus.Available);
        f.Db.Set<AccessCode>().Should().OnlyContain(a => !a.IsActive);
    }

    [Theory]
    [InlineData(ContractStatus.Draft)]
    [InlineData(ContractStatus.Terminated)]
    [InlineData(ContractStatus.Closed)]
    public async Task Terminate_refuses_draft_and_finished_contracts(ContractStatus status)
    {
        var f = Create(Roles.FacilityManager);
        Seed(f.Db);
        f.Db.Set<Contract>().Single().Status = status;
        f.Db.SaveChanges();

        (await f.Contracts.TerminateAsync(1, default)).Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Export_quotes_fields_and_defuses_formulas()
    {
        var f = Create();
        Seed(f.Db);
        f.Db.Set<User>().Single().FullName = "=HYPERLINK(\"x\")";
        f.Db.SaveChanges();

        var csv = (await f.Contracts.ExportCsvAsync(null, default)).Value!;

        csv.Should().StartWith("ContractNumber,Customer,UnitCode,StartDate,EndDate,MonthlyRent,Status\n");
        csv.Should().Contain("\"CT1\",\"'=HYPERLINK(\"\"x\"\")\",\"A-101\",2026-10-13,2027-01-13,0,Draft");
    }
}
