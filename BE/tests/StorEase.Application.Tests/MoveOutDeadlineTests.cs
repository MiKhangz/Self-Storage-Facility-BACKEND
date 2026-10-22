using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Billing;
using StorEase.Application.Features.Returns;
using StorEase.Application.Features.Returns.Dtos;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

/// <summary>A customer who asks to return the unit has 15 days from the request to empty it.</summary>
public class MoveOutDeadlineTests
{
    private static int SeedActiveContract(TestDb t)
    {
        var today = VietnamTime.Today;
        var contract = new Contract
        {
            Reservation = new Reservation
            {
                CustomerId = CustomerId, FacilityId = FacilityId, UnitTypeId = SmallTypeId, StorageUnitId = A101, ReservationCode = "RS-MOVE",
                MoveInDate = today.AddDays(-30), RentalMonths = 3, Status = ReservationStatus.HandedOver, CreatedAt = DateTime.UtcNow,
            },
            CustomerId = CustomerId, StorageUnitId = A101, ContractNumber = "CT-MOVE", StartDate = today.AddDays(-30),
            EndDate = today.AddDays(60), MonthlyRent = 900_000m, DepositAmount = 900_000m, PaymentDay = 10,
            SignedAt = DateTime.UtcNow, Status = ContractStatus.Active,
        };
        contract.AccessCodes.Add(new AccessCode { CodeHash = "x", IssuedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(60), IsActive = true });
        t.Db.Add(contract);
        t.Db.SaveChanges();
        t.Db.ChangeTracker.Clear();
        return contract.ContractId;
    }

    private static CreateReturnRequest On(int contractId, int days) =>
        new(contractId, VietnamTime.Today.AddDays(days), null, RefundMethod.Cash, null);

    [Fact]
    public async Task The_return_date_must_fall_within_15_days_of_the_request()
    {
        using var t = Create();
        var contractId = SeedActiveContract(t);
        var returns = new ReturnService(t.Uow, Customer);

        var tooLate = await returns.RequestAsync(On(contractId, 16), default);
        var lastDay = await returns.RequestAsync(On(contractId, 15), default);

        tooLate.Error!.Type.Should().Be(ErrorType.Validation);
        lastDay.IsSuccess.Should().BeTrue(lastDay.Error?.Message);
        lastDay.Value!.MoveOutDeadline.Should().Be(VietnamTime.Today.AddDays(ReturnService.MoveOutDays));
        lastDay.Value.AccessLockedAt.Should().BeNull();
    }

    [Fact]
    public async Task The_daily_job_locks_access_once_the_deadline_has_passed()
    {
        using var t = Create();
        var contractId = SeedActiveContract(t);
        var id = (await new ReturnService(t.Uow, Customer).RequestAsync(On(contractId, 10), default)).Value!.UnitReturnId;
        var billing = new BillingCycleService(t.Uow, new ReminderService(t.Uow, Staff, new FakeEmailService()));
        var deadline = VietnamTime.Today.AddDays(ReturnService.MoveOutDays);

        var onDeadline = await billing.RunAsync(deadline, default);
        var dayAfter = await billing.RunAsync(deadline.AddDays(1), default);
        var again = await billing.RunAsync(deadline.AddDays(2), default);

        onDeadline.Value!.ReturnsLockedOut.Should().Be(0, "the deadline day itself is still the customer's");
        dayAfter.Value!.ReturnsLockedOut.Should().Be(1);
        again.Value!.ReturnsLockedOut.Should().Be(0, "a locked return is not locked twice");
        (await t.Db.AccessCodes.SingleAsync(a => a.ContractId == contractId)).IsActive.Should().BeFalse();
        (await new ReturnService(t.Uow, Staff).GetAsync(id, default)).Value!.AccessLockedAt.Should().NotBeNull();
    }
}
