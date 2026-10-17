using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StorEase.Application.Common;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Infrastructure.Persistence.Seed;

/// <summary>
/// Demo data for the core rental flow (DEMO_PLAN): roles, five accounts (password <see cref="DemoPassword"/>),
/// facility "StorEase Gò Vấp" with one floor and six units, two unit types and an Active pricing policy.
/// Idempotent: every row is looked up by its natural key first, so running it twice changes nothing.
/// </summary>
public class DbSeeder(AppDbContext db, IPasswordHasher hasher, ILogger<DbSeeder> logger)
{
    public const string DemoPassword = "Demo@123";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if ((await db.Database.GetPendingMigrationsAsync(ct)).Any())
        {
            logger.LogWarning("Demo data not seeded: the database has pending migrations. Run `dotnet ef database update` first.");
            return;
        }

        var roles = new Dictionary<string, Role>();
        foreach (var name in new[] { Roles.StorageCustomer, Roles.FacilityStaff, Roles.FacilityManager, Roles.BusinessOperationsManager, Roles.SystemAdministrator })
            roles[name] = await db.Roles.FirstOrDefaultAsync(r => r.RoleName == name, ct) ?? db.Roles.Add(new Role { RoleName = name }).Entity;

        var customer = await UserAsync("customer@storease.vn", "Khách Demo", "0900000001", roles[Roles.StorageCustomer], ct);
        var staff = await UserAsync("staff@storease.vn", "Nhân viên Demo", "0900000002", roles[Roles.FacilityStaff], ct);
        var manager = await UserAsync("manager@storease.vn", "Quản lý Demo", "0900000003", roles[Roles.FacilityManager], ct);
        var ops = await UserAsync("ops@storease.vn", "Vận hành Demo", "0900000004", roles[Roles.BusinessOperationsManager], ct);
        await UserAsync("admin@storease.vn", "Quản trị Demo", "0900000005", roles[Roles.SystemAdministrator], ct);
        customer.CitizenId ??= "079000000001";
        await db.SaveChangesAsync(ct);

        var facility = await db.Facilities.FirstOrDefaultAsync(f => f.Code == "GV", ct)
            ?? db.Facilities.Add(new Facility
            {
                Code = "GV", Name = "StorEase Gò Vấp", Address = "1 Đường Demo, Phường 10", District = "Gò Vấp",
                Phone = "0281234567", OpeningHours = "06:00-22:00", Status = FacilityStatus.Active, Manager = manager,
            }).Entity;
        await db.SaveChangesAsync(ct);

        foreach (var user in new[] { staff, manager })
            if (!await db.StaffFacilities.AnyAsync(s => s.UserId == user.UserId && s.FacilityId == facility.FacilityId, ct))
                db.StaffFacilities.Add(new StaffFacility { UserId = user.UserId, FacilityId = facility.FacilityId, AssignedDate = new DateOnly(2026, 10, 1) });

        var floor = await db.Floors.FirstOrDefaultAsync(f => f.FacilityId == facility.FacilityId && f.FloorNumber == 1, ct)
            ?? db.Floors.Add(new Floor { FacilityId = facility.FacilityId, FloorNumber = 1, Name = "Tầng 1" }).Entity;
        var small = await UnitTypeAsync("S5", "5 m²", 5, 2, 2.5m, ct);
        var large = await UnitTypeAsync("M10", "10 m²", 10, 2.5m, 4, ct);
        await db.SaveChangesAsync(ct);

        foreach (var (code, type, price) in new[]
                 {
                     ("A-101", small, 900_000m), ("A-102", small, 900_000m), ("A-103", small, 900_000m),
                     ("A-104", large, 1_600_000m), ("A-105", large, 1_600_000m), ("A-106", large, 1_600_000m),
                 })
            if (!await db.StorageUnits.AnyAsync(u => u.FacilityId == facility.FacilityId && u.UnitCode == code, ct))
                db.StorageUnits.Add(new StorageUnit
                {
                    FacilityId = facility.FacilityId, FloorId = floor.FloorId, UnitTypeId = type.UnitTypeId,
                    UnitCode = code, Zone = "A", Status = StorageUnitStatus.Available, CurrentPrice = price,
                });

        if (!await db.PricingPolicies.AnyAsync(ct))
        {
            var policy = db.PricingPolicies.Add(new PricingPolicy
            {
                CreatedBy = ops.UserId, Version = 1, EffectiveFrom = new DateOnly(2026, 10, 1), DepositMonths = 1,
                GraceDays = 5, NoticeDays = 30, LateFeePercentPerWeek = 2, SuspendAfterDays = 14, ClearOutAfterDays = 60,
                CancelFreeHours = 24, Status = PricingPolicyStatus.Active,
            }).Entity;
            policy.UnitPrices.Add(new UnitPrice { UnitTypeId = small.UnitTypeId, BasePricePerMonth = 900_000m, Discount3Month = 5, Discount6Month = 10, Discount12Month = 15 });
            policy.UnitPrices.Add(new UnitPrice { UnitTypeId = large.UnitTypeId, BasePricePerMonth = 1_600_000m, Discount3Month = 5, Discount6Month = 10, Discount12Month = 15 });
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Demo data ready. Accounts use password {Password}.", DemoPassword);
    }

    private async Task<User> UserAsync(string email, string name, string phone, Role role, CancellationToken ct) =>
        await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct)
        ?? db.Users.Add(new User
        {
            Email = email, FullName = name, PhoneNumber = phone, Role = role, PasswordHash = hasher.Hash(DemoPassword),
            Status = UserStatus.Active, CreatedAt = DateTime.UtcNow,
        }).Entity;

    private async Task<UnitType> UnitTypeAsync(string code, string name, decimal area, decimal width, decimal depth, CancellationToken ct) =>
        await db.UnitTypes.FirstOrDefaultAsync(t => t.Code == code, ct)
        ?? db.UnitTypes.Add(new UnitType { Code = code, Name = name, AreaM2 = area, Width = width, Depth = depth, Height = 2.4m }).Entity;
}
