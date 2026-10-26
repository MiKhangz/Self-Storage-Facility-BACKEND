using FluentAssertions;
using StorEase.Application.Common;
using StorEase.Application.Features.UnitVisits;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

public class UnitVisitTests
{
    private static int SeedContract(TestDb t, ContractStatus status)
    {
        var contract = new Contract
        {
            Reservation = new Reservation
            {
                CustomerId = CustomerId, FacilityId = FacilityId, UnitTypeId = SmallTypeId, StorageUnitId = A101,
                ReservationCode = "RS-VISIT", MoveInDate = MoveIn, RentalMonths = 1,
                Status = ReservationStatus.CheckedIn, CreatedAt = DateTime.UtcNow,
            },
            CustomerId = CustomerId, StorageUnitId = A101, ContractNumber = "CT-VISIT", StartDate = MoveIn,
            EndDate = MoveIn.AddMonths(1), MonthlyRent = 900_000m, DepositAmount = 900_000m, PaymentDay = 10,
            SignedAt = DateTime.UtcNow, Status = status,
        };
        t.Db.Add(contract);
        t.Db.SaveChanges();
        t.Db.ChangeTracker.Clear();
        return contract.ContractId;
    }

    [Fact]
    public async Task Visit_requires_customer_pins_that_expire_and_can_only_be_used_once()
    {
        using var t = Create();
        var contractId = SeedContract(t, ContractStatus.Active);
        var customer = new UnitVisitService(t.Uow, Customer);
        var staff = new UnitVisitService(t.Uow, Staff);

        var entryPin = await customer.IssueCheckInPinAsync(contractId, default);
        entryPin.Value!.Pin.Should().MatchRegex("^\\d{6}$");
        entryPin.Value.ExpiredAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(3), TimeSpan.FromSeconds(5));
        var identity = await staff.VerifyCheckInPinAsync(entryPin.Value.Pin, default);
        identity.Value!.UnitCode.Should().Be("A-101");
        var checkIn = await staff.CheckInAsync(new UnitVisitCheckInRequest(entryPin.Value.Pin, "Gate entry"), default);
        checkIn.IsSuccess.Should().BeTrue(checkIn.Error?.Message);
        checkIn.Value!.CustomerPhone.Should().NotBeNullOrWhiteSpace();
        (await staff.CheckInAsync(new UnitVisitCheckInRequest(entryPin.Value.Pin, null), default))
            .Error!.Type.Should().Be(ErrorType.NotFound, "the PIN has already been used");
        (await customer.IssueCheckInPinAsync(contractId, default)).Error!.Type.Should().Be(ErrorType.Conflict);

        // Staff can complete checkout directly from the selected active visit; no customer PIN is required.
        var checkOut = await staff.CheckOutAsync(checkIn.Value.UnitVisitId, default);
        checkOut.Value!.CheckOutAt.Should().NotBeNull();
        checkOut.Value.CheckedOutBy.Should().Be(StaffId);
        (await staff.CheckOutAsync(checkIn.Value.UnitVisitId, default))
            .Error!.Type.Should().Be(ErrorType.Conflict);
        (await customer.IssueCheckInPinAsync(contractId, default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task A_contract_that_is_not_active_cannot_request_a_visit_pin()
    {
        using var t = Create();
        var contractId = SeedContract(t, ContractStatus.Suspended);

        var result = await new UnitVisitService(t.Uow, Customer).IssueCheckInPinAsync(contractId, default);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task A_staff_member_outside_the_facility_cannot_verify_a_pin()
    {
        using var t = Create();
        var contractId = SeedContract(t, ContractStatus.Active);
        var pin = await new UnitVisitService(t.Uow, Customer).IssueCheckInPinAsync(contractId, default);
        var outsider = new UnitVisitService(t.Uow, new FakeCurrentUser(77, Roles.FacilityStaff));

        var result = await outsider.VerifyCheckInPinAsync(pin.Value!.Pin, default);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }
}
