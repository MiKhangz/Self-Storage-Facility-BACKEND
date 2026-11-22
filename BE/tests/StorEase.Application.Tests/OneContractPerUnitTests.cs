using FluentAssertions;
using StorEase.Application.Common;
using StorEase.Application.Features.AccessCodes;
using StorEase.Application.Features.Contracts;
using StorEase.Application.Features.Contracts.Dtos;
using StorEase.Application.Features.Renewals;
using StorEase.Application.Features.Reservations;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using StorEase.Infrastructure.Services;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

/// <summary>A second contract on a unit makes the save fail as soon as the first turns Active or Overstay again.</summary>
public class OneContractPerUnitTests
{
    private static readonly DateOnly Today = VietnamTime.Today;

    private static ContractService Contracts(TestDb t) =>
        new(t.Uow, Staff, new AccessCodeService(t.Uow, new Pbkdf2PasswordHasher(), Staff, TestCodes.Protector));

    private static Contract Rent(TestDb t, int unitId, DateOnly start, DateOnly end, ContractStatus status, int customerId = CustomerId)
    {
        var contract = new Contract
        {
            Reservation = new Reservation
            {
                CustomerId = customerId, FacilityId = FacilityId, UnitTypeId = SmallTypeId, StorageUnitId = unitId,
                ReservationCode = $"RS-ONE{unitId}", MoveInDate = start, RentalMonths = 1, CreatedAt = DateTime.UtcNow,
                Status = status == ContractStatus.Draft ? ReservationStatus.Confirmed : ReservationStatus.HandedOver,
            },
            CustomerId = customerId, StorageUnitId = unitId, ContractNumber = $"CT-ONE{unitId}", StartDate = start, EndDate = end,
            MonthlyRent = 900_000m, DepositAmount = 900_000m, PaymentDay = 5, Status = status,
        };
        t.Db.Add(contract);
        t.Db.SaveChanges();
        return contract;
    }

    private static Reservation Book(TestDb t, int unitId, int? offeredUnitId = null)
    {
        var booking = new Reservation
        {
            CustomerId = OtherCustomerId, FacilityId = FacilityId, UnitTypeId = SmallTypeId, StorageUnitId = unitId, ReservationCode = "RS-SECOND",
            MoveInDate = Today.AddDays(30), RentalMonths = 1, Status = ReservationStatus.Confirmed, CreatedAt = DateTime.UtcNow,
            ProposedStorageUnitId = offeredUnitId,
        };
        t.Db.Add(booking);
        t.Db.SaveChanges();
        return booking;
    }

    [Theory]
    [InlineData(ContractStatus.Overdue)]
    [InlineData(ContractStatus.Suspended)]
    [InlineData(ContractStatus.PendingReturn)]
    [InlineData(ContractStatus.Overstay)]
    public async Task No_contract_is_drafted_while_another_still_holds_the_unit(ContractStatus status)
    {
        using var t = Create();
        Rent(t, A101, Today.AddDays(-30), Today.AddDays(5), status);
        var next = Book(t, A101);

        var drafted = await Contracts(t).CreateAsync(new CreateContractRequest(next.ReservationId, 5), default);

        drafted.Error!.Message.Should().Be("The unit already has a contract that has not ended.");
    }

    [Theory]
    [InlineData(ContractStatus.Overdue)]
    [InlineData(ContractStatus.Suspended)]
    [InlineData(ContractStatus.PendingReturn)]
    public async Task A_draft_does_not_follow_its_booking_onto_a_unit_still_under_contract(ContractStatus status)
    {
        using var t = Create();
        Rent(t, A102, Today.AddDays(-30), Today.AddDays(5), status);
        var next = Book(t, A101, offeredUnitId: A102);
        var draft = new Contract
        {
            ReservationId = next.ReservationId, CustomerId = OtherCustomerId, StorageUnitId = A101, ContractNumber = "CT-SECOND",
            StartDate = next.MoveInDate, EndDate = next.MoveInDate.AddMonths(1), PaymentDay = 5, Status = ContractStatus.Draft,
        };
        t.Db.Add(draft);
        t.Db.SaveChanges();

        var accepted = await new ReservationService(t.Uow, OtherCustomer, new FakeEmailService())
            .AcceptProposedUnitAsync(next.ReservationId, default);

        accepted.IsSuccess.Should().BeTrue(accepted.Error?.Message);
        t.Db.ChangeTracker.Clear();
        t.Db.Contracts.Find(draft.ContractId)!.Status.Should().Be(ContractStatus.Terminated);
    }

    /// <summary>A-101 is booked next, so renewing needs a move; A-102 is drafted for a rental starting after the renewed period.</summary>
    private static Renewal RenewalNeedingAMove(TestDb t)
    {
        var end = Today.AddDays(5);
        var renter = Rent(t, A101, end.AddMonths(-1), end, ContractStatus.Active);
        Rent(t, A102, end.AddMonths(3), end.AddMonths(4), ContractStatus.Draft, OtherCustomerId);
        t.Db.Add(new Reservation
        {
            CustomerId = OtherCustomerId, FacilityId = FacilityId, UnitTypeId = SmallTypeId, StorageUnitId = A101, ReservationCode = "RS-NEXT",
            MoveInDate = end.AddDays(1), RentalMonths = 1, Status = ReservationStatus.Confirmed, CreatedAt = DateTime.UtcNow,
        });
        var renewal = new Renewal
        {
            Contract = renter, RequestedAt = DateTime.UtcNow, ExtendMonths = 1, NewEndDate = end.AddMonths(1), NewMonthlyRent = 900_000m,
            Status = RenewalStatus.Requested,
        };
        t.Db.Add(renewal);
        t.Db.SaveChanges();
        return renewal;
    }

    [Fact]
    public async Task A_renewal_is_not_offered_a_unit_drafted_for_someone_else()
    {
        using var t = Create();
        var renewal = RenewalNeedingAMove(t);

        var approved = await new RenewalService(t.Uow, Staff).ApproveAsync(renewal.RenewalId, default);

        approved.Error!.Message.Should().Contain("reject the renewal", "A-102 is the only other unit and it has a contract");
    }

    [Fact]
    public async Task A_unit_drafted_after_the_offer_cannot_be_accepted()
    {
        using var t = Create();
        var renewal = RenewalNeedingAMove(t);
        renewal.Status = RenewalStatus.AwaitingCustomer;
        renewal.TransferStorageUnitId = A102;
        t.Db.SaveChanges();

        var accepted = await new RenewalService(t.Uow, Customer).AcceptTransferAsync(renewal.RenewalId, default);

        accepted.Error!.Message.Should().Contain("taken meanwhile");
    }
}
