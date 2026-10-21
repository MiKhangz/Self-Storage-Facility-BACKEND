using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using StorEase.Infrastructure.Persistence;
using StorEase.Infrastructure.Services;

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? "Server=localhost\\SQLEXPRESS;Database=StorEase;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlServer(connectionString, sql => sql.MigrationsAssembly("StorEase.Infrastructure"))
    .Options;

await using var db = new AppDbContext(options);
if (args.Contains("--print-demo-hash", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine(new Pbkdf2PasswordHasher().Hash("Demo@123"));
    return;
}

if (args.Contains("--export-sql", StringComparer.OrdinalIgnoreCase))
{
    var migrator = db.Database.GetService<IMigrator>();
    Console.WriteLine(migrator.GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent));
    return;
}

Console.WriteLine("Applying StorEase migrations...");
await db.Database.MigrateAsync();
Console.WriteLine("Seeding demo data...");

var roles = new Dictionary<string, Role>
{
    ["StorageCustomer"] = await EnsureRoleAsync("StorageCustomer", "Customer account"),
    ["FacilityStaff"] = await EnsureRoleAsync("FacilityStaff", "Facility staff account"),
    ["FacilityManager"] = await EnsureRoleAsync("FacilityManager", "Facility manager account"),
    ["BusinessOperationsManager"] = await EnsureRoleAsync("BusinessOperationsManager", "Pricing and operations account"),
    ["SystemAdministrator"] = await EnsureRoleAsync("SystemAdministrator", "System administrator account"),
};
await db.SaveChangesAsync();

var users = new Dictionary<string, User>
{
    ["customer@storease.vn"] = await EnsureUserAsync("customer@storease.vn", "Demo Customer", roles["StorageCustomer"]),
    ["staff@storease.vn"] = await EnsureUserAsync("staff@storease.vn", "Demo Staff", roles["FacilityStaff"]),
    ["manager@storease.vn"] = await EnsureUserAsync("manager@storease.vn", "Demo Facility Manager", roles["FacilityManager"]),
    ["ops@storease.vn"] = await EnsureUserAsync("ops@storease.vn", "Demo Operations Manager", roles["BusinessOperationsManager"]),
    ["admin@storease.vn"] = await EnsureUserAsync("admin@storease.vn", "Demo Administrator", roles["SystemAdministrator"]),
};
await db.SaveChangesAsync();

var facility = await db.Facilities.FirstOrDefaultAsync(f => f.Code == "DEMO-GV");
if (facility is null)
{
    facility = new Facility
    {
        Code = "DEMO-GV",
        Name = "StorEase Demo Go Vap",
        Address = "123 Demo Street",
        District = "Go Vap",
        Phone = "0900000000",
        OpeningHours = "08:00-20:00",
        ManagerId = users["manager@storease.vn"].UserId,
        Status = FacilityStatus.Active,
    };
    db.Facilities.Add(facility);
    await db.SaveChangesAsync();
}

var floor = await db.Floors.FirstOrDefaultAsync(f => f.FacilityId == facility.FacilityId && f.FloorNumber == 1);
if (floor is null)
{
    floor = new Floor { FacilityId = facility.FacilityId, FloorNumber = 1, Name = "Ground Floor" };
    db.Floors.Add(floor);
    await db.SaveChangesAsync();
}

var small = await EnsureUnitTypeAsync("S5", "Small 5m2", 5, 2, 2.5m, 2.2m, false);
var medium = await EnsureUnitTypeAsync("M10", "Medium 10m2", 10, 2.5m, 4, 2.2m, true);
await db.SaveChangesAsync();

for (var i = 1; i <= 4; i++)
    await EnsureUnitAsync($"A-10{i}", i <= 2 ? small.UnitTypeId : medium.UnitTypeId, i <= 2 ? 900_000m : 1_500_000m);
await db.SaveChangesAsync();

var policy = await db.PricingPolicies.FirstOrDefaultAsync(p => p.Version == 1);
if (policy is null)
{
    policy = new PricingPolicy
    {
        CreatedBy = users["ops@storease.vn"].UserId,
        Version = 1,
        EffectiveFrom = new DateOnly(2026, 1, 1),
        DepositMonths = 1,
        GraceDays = 3,
        NoticeDays = 30,
        LateFeePercentPerWeek = 2,
        SuspendAfterDays = 14,
        ClearOutAfterDays = 60,
        CancelFreeHours = 24,
        Status = PricingPolicyStatus.Active,
    };
    db.PricingPolicies.Add(policy);
    await db.SaveChangesAsync();
}

await EnsureUnitPriceAsync(policy, small, 900_000m, 5, 10, 15);
await EnsureUnitPriceAsync(policy, medium, 1_500_000m, 5, 10, 15);
if (!await db.FeeTypes.AnyAsync(f => f.PricingPolicyId == policy.PricingPolicyId && f.Code == "SETUP"))
    db.FeeTypes.Add(new FeeType { PricingPolicyId = policy.PricingPolicyId, Code = "SETUP", Name = "Setup fee", Amount = 100_000m, IsPercentage = false });
await db.SaveChangesAsync();

var customer = users["customer@storease.vn"];
if (!await db.Invoices.AnyAsync(i => i.InvoiceNumber == "INV-DEMO-001"))
{
    var invoice = new Invoice
    {
        CustomerId = customer.UserId,
        InvoiceNumber = "INV-DEMO-001",
        IssueDate = DateOnly.FromDateTime(DateTime.UtcNow),
        DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
        TotalAmount = 1_000_000m,
        PaidAmount = 0m,
        Status = InvoiceStatus.Issued,
    };
    invoice.InvoiceItems.Add(new InvoiceItem
    {
        Description = "Demo monthly rent",
        Quantity = 1,
        UnitAmount = 1_000_000m,
        LineAmount = 1_000_000m,
    });
    db.Invoices.Add(invoice);
    await db.SaveChangesAsync();
}

await EnsureStaffFacilityAsync(users["staff@storease.vn"], facility);
await EnsureStaffFacilityAsync(users["manager@storease.vn"], facility);
await db.SaveChangesAsync();

Console.WriteLine();
Console.WriteLine("Demo database is ready.");
Console.WriteLine("Database: StorEase");
Console.WriteLine("Facility: DEMO-GV");
Console.WriteLine("Accounts: customer@storease.vn, staff@storease.vn, manager@storease.vn, ops@storease.vn, admin@storease.vn");
Console.WriteLine("Password for all demo accounts: Demo@123");
Console.WriteLine("Invoice for payment test: INV-DEMO-001");

async Task<Role> EnsureRoleAsync(string name, string description)
{
    var role = await db.Roles.FirstOrDefaultAsync(r => r.RoleName == name);
    if (role is not null)
        return role;
    role = new Role { RoleName = name, Description = description };
    db.Roles.Add(role);
    return role;
}

async Task<User> EnsureUserAsync(string email, string fullName, Role role)
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
    if (user is not null)
        return user;
    user = new User
    {
        Role = role,
        FullName = fullName,
        Email = email,
        PhoneNumber = "0900000000",
        PasswordHash = new Pbkdf2PasswordHasher().Hash("Demo@123"),
        Status = UserStatus.Active,
        CreatedAt = DateTime.UtcNow,
    };
    db.Users.Add(user);
    return user;
}

async Task<UnitType> EnsureUnitTypeAsync(string code, string name, decimal area, decimal width, decimal depth, decimal height, bool climate)
{
    var type = await db.UnitTypes.FirstOrDefaultAsync(t => t.Code == code);
    if (type is not null)
        return type;
    type = new UnitType { Code = code, Name = name, AreaM2 = area, Width = width, Depth = depth, Height = height, IsClimateControlled = climate };
    db.UnitTypes.Add(type);
    return type;
}

async Task EnsureUnitAsync(string code, int unitTypeId, decimal price)
{
    if (await db.StorageUnits.AnyAsync(u => u.FacilityId == facility.FacilityId && u.UnitCode == code))
        return;
    db.StorageUnits.Add(new StorageUnit
    {
        FacilityId = facility.FacilityId,
        FloorId = floor.FloorId,
        UnitTypeId = unitTypeId,
        UnitCode = code,
        Zone = "A",
        Status = StorageUnitStatus.Available,
        CurrentPrice = price,
    });
}

async Task EnsureUnitPriceAsync(PricingPolicy pricingPolicy, UnitType type, decimal basePrice, decimal d3, decimal d6, decimal d12)
{
    var price = await db.UnitPrices.FirstOrDefaultAsync(p => p.PricingPolicyId == pricingPolicy.PricingPolicyId && p.UnitTypeId == type.UnitTypeId);
    if (price is not null)
        return;
    db.UnitPrices.Add(new UnitPrice
    {
        PricingPolicyId = pricingPolicy.PricingPolicyId,
        UnitTypeId = type.UnitTypeId,
        BasePricePerMonth = basePrice,
        Discount3Month = d3,
        Discount6Month = d6,
        Discount12Month = d12,
    });
}

async Task EnsureStaffFacilityAsync(User user, Facility assignedFacility)
{
    if (await db.StaffFacilities.AnyAsync(s => s.UserId == user.UserId && s.FacilityId == assignedFacility.FacilityId))
        return;
    db.StaffFacilities.Add(new StaffFacility
    {
        UserId = user.UserId,
        FacilityId = assignedFacility.FacilityId,
        AssignedDate = DateOnly.FromDateTime(DateTime.UtcNow),
    });
}
