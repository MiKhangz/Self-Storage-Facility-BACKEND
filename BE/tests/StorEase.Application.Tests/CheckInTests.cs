using FluentAssertions;
using StorEase.Application.Common;
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
        var staff = new CheckInService(t.Uow, Staff, Hasher);

        var first = await staff.CheckInAsync(new CheckInRequest(contractId, "Lấy đồ"), default);
        var second = await staff.CheckInAsync(new CheckInRequest(contractId, null), default);
        var outOnce = await staff.CheckOutAsync(first.Value!.CheckInId, default);
        var outTwice = await staff.CheckOutAsync(first.Value.CheckInId, default);
        var afterLeaving = await staff.CheckInAsync(new CheckInRequest(contractId, null), default);
        var outsider = await new CheckInService(t.Uow, new FakeCurrentUser(77, Roles.FacilityStaff), Hasher)
            .CheckInAsync(new CheckInRequest(contractId, null), default);
        var customerSees = await new CheckInService(t.Uow, Customer, Hasher).ListAsync(contractId, 1, 20, default);

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

        var result = await new CheckInService(t.Uow, Staff, Hasher).CheckInAsync(new CheckInRequest(contractId, null), default);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task The_desk_finds_a_renter_by_their_pin_and_the_board_shows_who_is_inside()
    {
        using var t = Create();
        var contractId = SeedContract(t, ContractStatus.PendingReturn); // moving out: still allowed in
        var desk = new CheckInService(t.Uow, Staff, Hasher);

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
}
