using FluentAssertions;
using StorEase.Application.Common;
using StorEase.Application.Features.AccessCodes;
using StorEase.Application.Features.CheckIns;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using StorEase.Infrastructure.Services;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

/// <summary>
/// SR-05: one open visit per rental; staff of other facilities cannot record one. A customer enters only with a
/// working access code, which the desk finds by the PIN they say.
/// </summary>
public class CheckInTests
{
    private static readonly Pbkdf2PasswordHasher Hasher = new();
    private const string Pin = "482915";

    private static AccessCodeService Codes(TestDb t) => new(t.Uow, Hasher, Staff, TestCodes.Protector);

    private static int SeedContract(TestDb t, ContractStatus status, bool codeWorks = true)
    {
        var contract = new Contract
        {
            Reservation = new Reservation
            {
                CustomerId = CustomerId, FacilityId = FacilityId, UnitTypeId = SmallTypeId, StorageUnitId = A101, ReservationCode = "RS-VISIT",
                MoveInDate = MoveIn, RentalMonths = 1, Status = ReservationStatus.HandedOver, CreatedAt = DateTime.UtcNow,
            },
            CustomerId = CustomerId, StorageUnitId = A101, ContractNumber = "CT-VISIT", StartDate = MoveIn, EndDate = MoveIn.AddMonths(1),
            MonthlyRent = 900_000m, DepositAmount = 900_000m, PaymentDay = 10, SignedAt = DateTime.UtcNow, Status = status,
        };
        contract.AccessCodes.Add(new AccessCode
        {
            CodeHash = Hasher.Hash(Pin), IssuedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(30), IsActive = true,
            SuspendedAt = codeWorks ? null : DateTime.UtcNow,
        });
        t.Db.Add(contract);
        t.Db.SaveChanges();
        t.Db.ChangeTracker.Clear();
        return contract.ContractId;
    }

    [Fact]
    public async Task A_visit_opens_once_and_closes_once()
    {
        using var t = Create();
        var contractId = SeedContract(t, ContractStatus.Active);
        var staff = new CheckInService(t.Uow, Staff, Hasher, TestCodes.Protector, Codes(t));

        var first = await staff.CheckInAsync(new CheckInRequest(contractId, "Lấy đồ"), default);
        var second = await staff.CheckInAsync(new CheckInRequest(contractId, null), default);
        var outOnce = await staff.CheckOutAsync(first.Value!.CheckInId, default);
        var outTwice = await staff.CheckOutAsync(first.Value.CheckInId, default);
        var afterLeaving = await staff.CheckInAsync(new CheckInRequest(contractId, null), default);
        var outsider = await new CheckInService(t.Uow, new FakeCurrentUser(77, Roles.FacilityStaff), Hasher, TestCodes.Protector, Codes(t))
            .CheckInAsync(new CheckInRequest(contractId, null), default);
        var customerSees = await new CheckInService(t.Uow, Customer, Hasher, TestCodes.Protector, Codes(t)).ListAsync(contractId, 1, 20, default);

        first.IsSuccess.Should().BeTrue(first.Error?.Message);
        first.Value.UnitCode.Should().Be("A-101");
        second.Error!.Type.Should().Be(ErrorType.Conflict, "the customer is still inside");
        outOnce.Value!.CheckOutAt.Should().NotBeNull();
        outTwice.Error!.Type.Should().Be(ErrorType.Conflict);
        afterLeaving.IsSuccess.Should().BeTrue();
        outsider.Error!.Type.Should().Be(ErrorType.NotFound, "staff 77 works at no facility");
        customerSees.Value!.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task A_customer_locked_out_for_unpaid_rent_cannot_visit()
    {
        using var t = Create();
        var contractId = SeedContract(t, ContractStatus.Suspended, codeWorks: false);

        var result = await new CheckInService(t.Uow, Staff, Hasher, TestCodes.Protector, Codes(t)).CheckInAsync(new CheckInRequest(contractId, null), default);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task The_desk_finds_a_renter_by_their_pin_and_the_board_shows_who_is_inside()
    {
        using var t = Create();
        var contractId = SeedContract(t, ContractStatus.PendingReturn); // moving out: still allowed in
        var desk = new CheckInService(t.Uow, Staff, Hasher, TestCodes.Protector, Codes(t));

        var wrong = await desk.FindByAccessCodeAsync(new FindByAccessCodeRequest("000000"), default);
        var found = await desk.FindByAccessCodeAsync(new FindByAccessCodeRequest(Pin), default);
        var visit = await desk.CheckInAsync(new CheckInRequest(found.Value!.ContractId, null), default);
        var board = await desk.BoardAsync(default);

        wrong.Error!.Type.Should().Be(ErrorType.NotFound);
        found.Value.ContractId.Should().Be(contractId);
        found.Value.Inside.Should().BeFalse();
        visit.IsSuccess.Should().BeTrue(visit.Error?.Message);
        board.Value!.Single().Inside.Should().BeTrue();
        board.Value.Single().OpenCheckInId.Should().Be(visit.Value!.CheckInId);
    }

    [Fact]
    public async Task Staff_close_a_forgotten_visit_with_a_reason_and_a_time_after_it_began()
    {
        using var t = Create();
        var contractId = SeedContract(t, ContractStatus.Active);
        var desk = new CheckInService(t.Uow, Staff, Hasher, TestCodes.Protector, Codes(t));
        var visit = (await desk.CheckInAsync(new CheckInRequest(contractId, null), default)).Value!;

        var future = await desk.MarkLeftAsync(visit.CheckInId, new MarkLeftRequest(DateTime.UtcNow.AddHours(2), "Forgot"), default);
        var marked = await desk.MarkLeftAsync(visit.CheckInId, new MarkLeftRequest(DateTime.UtcNow, "Forgot to check out"), default);

        future.Error!.Type.Should().Be(ErrorType.Validation);
        marked.Value!.CheckOutMethod.Should().Be(CheckOutMethod.MarkedByStaff);
        marked.Value.CheckOutReason.Should().Be("Forgot to check out");
        (await desk.BoardAsync(default)).Value!.Single().LeftHow.Should().Be(CheckOutMethod.MarkedByStaff);
    }

    [Fact]
    public async Task Visits_still_open_at_closing_time_are_closed_at_closing_time()
    {
        // 10:00 in Vietnam closes at 22:00 that day; 23:00 (after closing) at 22:00 the next day.
        var tenAm = new DateTime(2026, 10, 26, 3, 0, 0, DateTimeKind.Utc);
        CheckInService.ClosingAfter(tenAm, "06:00-22:00").Should().Be(new DateTime(2026, 10, 26, 15, 0, 0, DateTimeKind.Utc));
        CheckInService.ClosingAfter(tenAm.AddHours(13), "06:00-22:00").Should().Be(new DateTime(2026, 10, 27, 15, 0, 0, DateTimeKind.Utc));
        CheckInService.ClosingAfter(tenAm, null).Should().Be(new DateTime(2026, 10, 26, 16, 59, 0, DateTimeKind.Utc));

        using var t = Create();
        var contractId = SeedContract(t, ContractStatus.Active);
        t.Db.Add(new CheckIn { ContractId = contractId, StorageUnitId = A101, RecordedBy = StaffId, CheckInAt = tenAm });
        t.Db.SaveChanges();
        var desk = new CheckInService(t.Uow, Staff, Hasher, TestCodes.Protector, Codes(t));

        var beforeClosing = await desk.CloseAtClosingTimeAsync(tenAm.AddHours(5), default);
        var afterClosing = await desk.CloseAtClosingTimeAsync(tenAm.AddHours(14), default);

        beforeClosing.Should().Be(0);
        afterClosing.Should().Be(1);
        t.Db.ChangeTracker.Clear();
        var visit = t.Db.CheckIns.Single();
        visit.CheckOutMethod.Should().Be(CheckOutMethod.AutoAtClosing);
        visit.CheckOutAt.Should().Be(new DateTime(2026, 10, 26, 16, 59, 0, DateTimeKind.Utc), "the test facility has no hours set, so its day ends at 23:59");
    }

    [Fact]
    public async Task Each_visit_ends_with_a_new_code_so_an_overheard_one_stops_working()
    {
        using var t = Create();
        var contractId = SeedContract(t, ContractStatus.Active);
        var desk = new CheckInService(t.Uow, Staff, Hasher, TestCodes.Protector, Codes(t));
        var renter = new AccessCodeService(t.Uow, Hasher, Customer, TestCodes.Protector);
        var visit = (await desk.CheckInAsync(new CheckInRequest(contractId, null), default)).Value!;

        (await desk.FindByAccessCodeAsync(new FindByAccessCodeRequest(Pin), default)).IsSuccess.Should().BeTrue("the code still works during the visit");
        await desk.CheckOutAsync(visit.CheckInId, default);

        (await desk.FindByAccessCodeAsync(new FindByAccessCodeRequest(Pin), default)).Error!.Type.Should().Be(ErrorType.NotFound, "the old code died with the visit");
        var fresh = (await renter.GetActiveAsync(contractId, default)).Value!.Code;
        fresh.Should().NotBeNull().And.NotBe(Pin);
        (await desk.FindByAccessCodeAsync(new FindByAccessCodeRequest(fresh!), default)).Value!.ContractId.Should().Be(contractId);
    }

    [Fact]
    public async Task A_code_locked_for_unpaid_rent_is_not_renewed_when_a_visit_closes()
    {
        using var t = Create();
        var contractId = SeedContract(t, ContractStatus.Suspended, codeWorks: false);
        t.Db.Add(new CheckIn { ContractId = contractId, StorageUnitId = A101, RecordedBy = StaffId, CheckInAt = DateTime.UtcNow.AddHours(-1) });
        t.Db.SaveChanges();
        var desk = new CheckInService(t.Uow, Staff, Hasher, TestCodes.Protector, Codes(t));

        await desk.MarkLeftAsync(t.Db.CheckIns.Single().CheckInId, new MarkLeftRequest(DateTime.UtcNow, "Left"), default);

        t.Db.ChangeTracker.Clear();
        t.Db.AccessCodes.Where(a => a.ContractId == contractId).Should().ContainSingle().Which.SuspendedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Two_renters_with_the_same_code_are_never_guessed_between()
    {
        using var t = Create();
        SeedContract(t, ContractStatus.Active);
        var other = new Contract
        {
            Reservation = new Reservation
            {
                CustomerId = OtherCustomerId, FacilityId = FacilityId, UnitTypeId = SmallTypeId, StorageUnitId = A102, ReservationCode = "RS-TWIN",
                MoveInDate = MoveIn, RentalMonths = 1, Status = ReservationStatus.HandedOver, CreatedAt = DateTime.UtcNow,
            },
            CustomerId = OtherCustomerId, StorageUnitId = A102, ContractNumber = "CT-TWIN", StartDate = MoveIn, EndDate = MoveIn.AddMonths(1),
            MonthlyRent = 1m, DepositAmount = 1m, PaymentDay = 10, SignedAt = DateTime.UtcNow, Status = ContractStatus.Active,
        };
        other.AccessCodes.Add(new AccessCode
        {
            CodeHash = Hasher.Hash(Pin), CodeCipher = TestCodes.Protector.Protect(Pin), IssuedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30), IsActive = true,
        });
        t.Db.Add(other);
        t.Db.SaveChanges();

        var found = await new CheckInService(t.Uow, Staff, Hasher, TestCodes.Protector, Codes(t))
            .FindByAccessCodeAsync(new FindByAccessCodeRequest(Pin), default);

        found.Error!.Type.Should().Be(ErrorType.Conflict, "the desk must not pick one of two renters at random");
    }
}
