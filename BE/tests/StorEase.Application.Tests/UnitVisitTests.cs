using FluentAssertions;
using StorEase.Application.Common;
using StorEase.Application.Features.UnitVisits;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

/// <summary>SR-05: one open visit per Active contract; staff of other facilities cannot record one.</summary>
public class UnitVisitTests
{
    private static int SeedContract(TestDb t, ContractStatus status)
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
        t.Db.Add(contract);
        t.Db.SaveChanges();
        t.Db.ChangeTracker.Clear();
        return contract.ContractId;
    }

    [Fact]
    public async Task A_visit_opens_once_closes_once_and_needs_an_active_contract()
    {
        using var t = Create();
        var contractId = SeedContract(t, ContractStatus.Active);
        var staff = new UnitVisitService(t.Uow, Staff);

        var first = await staff.CheckInAsync(new UnitVisitCheckInRequest(contractId, "Lấy đồ"), default);
        var second = await staff.CheckInAsync(new UnitVisitCheckInRequest(contractId, null), default);
        var outOnce = await staff.CheckOutAsync(first.Value!.UnitVisitId, default);
        var outTwice = await staff.CheckOutAsync(first.Value.UnitVisitId, default);
        var afterLeaving = await staff.CheckInAsync(new UnitVisitCheckInRequest(contractId, null), default);
        var outsider = await new UnitVisitService(t.Uow, new FakeCurrentUser(77, Roles.FacilityStaff))
            .CheckInAsync(new UnitVisitCheckInRequest(contractId, null), default);
        var customerSees = await new UnitVisitService(t.Uow, Customer).ListAsync(contractId, 1, 20, default);

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
    public async Task A_contract_that_is_not_active_cannot_visit()
    {
        using var t = Create();
        var contractId = SeedContract(t, ContractStatus.Suspended);

        var result = await new UnitVisitService(t.Uow, Staff).CheckInAsync(new UnitVisitCheckInRequest(contractId, null), default);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
    }
}
