using StorEase.Application.Common;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Tests;

/// <summary>
/// The demo world for the core rental flow: one facility, two unit types, three units, a customer,
/// a second customer and a staff member. Ids are fixed so tests read like the demo script.
/// </summary>
public static class CoreFlowSeed
{
    public const int CustomerId = 1;
    public const int OtherCustomerId = 2;
    public const int StaffId = 3;
    public const int FacilityId = 1;
    public const int SmallTypeId = 1;   // 5 m²
    public const int LargeTypeId = 2;   // 10 m²
    public const int A101 = 1;          // small
    public const int A102 = 2;          // small
    public const int A104 = 3;          // large

    public static readonly DateOnly MoveIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

    public static TestDb Create()
    {
        var t = new TestDb();
        var customerRole = new Role { RoleName = Roles.StorageCustomer };
        var staffRole = new Role { RoleName = Roles.FacilityStaff };

        t.Db.AddRange(
            NewUser(CustomerId, customerRole, "customer@x.vn"),
            NewUser(OtherCustomerId, customerRole, "other@x.vn"),
            NewUser(StaffId, staffRole, "staff@x.vn"),
            new Facility { FacilityId = FacilityId, Code = "GV", Name = "Gò Vấp", Address = "1 Test", District = "Gò Vấp", Status = FacilityStatus.Active },
            new Floor { FloorId = 1, FacilityId = FacilityId, FloorNumber = 1 },
            new UnitType { UnitTypeId = SmallTypeId, Code = "S5", Name = "5 m²", AreaM2 = 5, Width = 2, Depth = 2.5m, Height = 2.4m },
            new UnitType { UnitTypeId = LargeTypeId, Code = "M10", Name = "10 m²", AreaM2 = 10, Width = 2.5m, Depth = 4, Height = 2.4m },
            NewUnit(A101, "A-101", SmallTypeId),
            NewUnit(A102, "A-102", SmallTypeId),
            NewUnit(A104, "A-104", LargeTypeId));
        t.Db.SaveChanges();
        t.Db.ChangeTracker.Clear();
        return t;
    }

    public static ICurrentUser Customer => new FakeCurrentUser(CustomerId, Roles.StorageCustomer);
    public static ICurrentUser OtherCustomer => new FakeCurrentUser(OtherCustomerId, Roles.StorageCustomer);
    public static ICurrentUser Staff => new FakeCurrentUser(StaffId, Roles.FacilityStaff);

    /// <summary>A Confirmed reservation holding unit A-101 (Reserved) and its Draft contract.</summary>
    public static (int ReservationId, int ContractId) SeedReadyForHandover(TestDb t, bool signed)
    {
        var reservation = new Reservation
        {
            CustomerId = CustomerId, FacilityId = FacilityId, UnitTypeId = SmallTypeId, StorageUnitId = A101,
            ReservationCode = "RS-TEST", MoveInDate = MoveIn, RentalMonths = 3,
            Status = ReservationStatus.Confirmed, CreatedAt = DateTime.UtcNow,
        };
        t.Db.Add(reservation);
        t.Db.SaveChanges();

        var contract = new Contract
        {
            ReservationId = reservation.ReservationId, CustomerId = CustomerId, StorageUnitId = A101,
            ContractNumber = "CT-TEST", StartDate = MoveIn, EndDate = MoveIn.AddMonths(3),
            MonthlyRent = 900_000m, DepositAmount = 900_000m, PaymentDay = 5,
            SignedAt = signed ? DateTime.UtcNow : null, Status = ContractStatus.Draft,
        };
        t.Db.Add(contract);
        t.Db.StorageUnits.Find(A101)!.Status = StorageUnitStatus.Reserved;
        t.Db.SaveChanges();
        t.Db.ChangeTracker.Clear();
        return (reservation.ReservationId, contract.ContractId);
    }

    private static User NewUser(int id, Role role, string email) => new()
    {
        UserId = id, Role = role, FullName = email, Email = email, PhoneNumber = "0900000000",
        PasswordHash = "x", Status = UserStatus.Active, CreatedAt = DateTime.UtcNow,
    };

    private static StorageUnit NewUnit(int id, string code, int typeId) => new()
    {
        StorageUnitId = id, FacilityId = FacilityId, FloorId = 1, UnitTypeId = typeId, UnitCode = code,
        Status = StorageUnitStatus.Available, CurrentPrice = typeId == SmallTypeId ? 900_000m : 1_600_000m,
    };
}
