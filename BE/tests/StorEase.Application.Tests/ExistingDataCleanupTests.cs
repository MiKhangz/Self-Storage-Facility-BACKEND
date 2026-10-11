using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using StorEase.Infrastructure.Migrations;

namespace StorEase.Application.Tests;

public class ExistingDataCleanupTests
{
    [Fact]
    public async Task Terminates_extra_open_contracts_keeping_the_active_one()
    {
        using var t = new TestDb();
        // Raw SQL: the entity model forbids the bad state through the unique index, so drop it first like an old database would have it.
        await t.Db.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS IX_Contract_StorageUnitId");
        await t.Db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=OFF");
        foreach (var (id, unit, status) in new[] { (1, 1, "Draft"), (2, 1, "Active"), (3, 1, "Draft"), (4, 2, "Draft"), (5, 2, "Closed"), (6, 3, "Active") })
            await t.Db.Database.ExecuteSqlRawAsync(
                "INSERT INTO Contract (ContractId, ReservationId, CustomerId, StorageUnitId, ContractNumber, StartDate, EndDate, MonthlyRent, DepositAmount, PaymentDay, Status) " +
                $"VALUES ({id}, {id}, 1, {unit}, 'C{id}', '2026-01-01', '2026-06-01', 1, 1, 5, '{status}')");

        await t.Db.Database.ExecuteSqlRawAsync(ExistingDataCleanup.TerminateExtraOpenContracts);

        var rows = await t.Db.Database.SqlQueryRaw<string>("SELECT ContractId || ':' || Status AS Value FROM Contract ORDER BY ContractId").ToListAsync();
        rows.Should().Equal("1:Terminated", "2:Active", "3:Terminated", "4:Draft", "5:Closed", "6:Active");
    }

    [Fact]
    public async Task Deduplicates_staff_facility_keeping_the_oldest_row()
    {
        using var t = new TestDb();
        await t.Db.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS IX_StaffFacility_UserId_FacilityId");
        await t.Db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=OFF");
        foreach (var (id, user, fac) in new[] { (1, 1, 1), (2, 1, 1), (3, 1, 2), (4, 2, 1), (5, 1, 1) })
            await t.Db.Database.ExecuteSqlRawAsync($"INSERT INTO StaffFacility (StaffFacilityId, UserId, FacilityId, AssignedDate) VALUES ({id}, {user}, {fac}, '2026-01-01')");

        await t.Db.Database.ExecuteSqlRawAsync(ExistingDataCleanup.DeduplicateStaffFacility);

        var ids = await t.Db.Database.SqlQueryRaw<int>("SELECT StaffFacilityId AS Value FROM StaffFacility ORDER BY StaffFacilityId").ToListAsync();
        ids.Should().Equal(1, 3, 4);
    }
}
