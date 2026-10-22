using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Returns;
using StorEase.Application.Features.Returns.Dtos;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

/// <summary>SR-04: no notice period; handing back before the end date keeps a share of the deposit.</summary>
public class ReturnSettlementTests
{
    private static int SeedActiveContract(TestDb t, decimal earlyTerminationPercent)
    {
        var today = VietnamTime.Today;
        t.Db.Add(new PricingPolicy
        {
            CreatedBy = StaffId, Version = 1, EffectiveFrom = new DateOnly(2025, 1, 1), DepositMonths = 1, GraceDays = 5,
            NoticeDays = 30, LateFeePercentPerWeek = 2, EarlyTerminationPercent = earlyTerminationPercent, SuspendAfterDays = 14,
            ClearOutAfterDays = 60, CancelFreeHours = 24, Status = PricingPolicyStatus.Active,
        });
        var contract = new Contract
        {
            Reservation = new Reservation
            {
                CustomerId = CustomerId, FacilityId = FacilityId, UnitTypeId = SmallTypeId, StorageUnitId = A101, ReservationCode = "RS-EARLY",
                MoveInDate = today.AddDays(-30), RentalMonths = 3, Status = ReservationStatus.HandedOver, CreatedAt = DateTime.UtcNow,
            },
            CustomerId = CustomerId, StorageUnitId = A101, ContractNumber = "CT-EARLY", StartDate = today.AddDays(-30),
            EndDate = today.AddDays(60), MonthlyRent = 900_000m, DepositAmount = 1_000_000m, PaymentDay = 10,
            SignedAt = DateTime.UtcNow, Status = ContractStatus.Active,
        };
        t.Db.Add(contract);
        t.Db.SaveChanges();
        t.Db.ChangeTracker.Clear();
        return contract.ContractId;
    }

    [Fact]
    public async Task An_early_handover_needs_no_notice_and_keeps_the_penalty_from_the_deposit()
    {
        using var t = Create();
        var contractId = SeedActiveContract(t, earlyTerminationPercent: 20);
        var ops = new FakeCurrentUser(98, Roles.BusinessOperationsManager);

        // Tomorrow is well inside the old 30-day notice period.
        var requested = await new ReturnService(t.Uow, Customer).RequestAsync(
            new CreateReturnRequest(contractId, VietnamTime.Today.AddDays(1), null, RefundMethod.Cash, null), default);
        var id = requested.Value!.UnitReturnId;
        await new ReturnService(t.Uow, Staff).ApproveAsync(id, new ApproveReturnRequest(DateTime.UtcNow.AddDays(1)), default);
        await new ReturnService(t.Uow, Staff).InspectAsync(id, new CreateReturnInspectionRequest(ConditionResult.Good, 50_000m, 0m, 0m), default);
        var settled = await new ReturnService(t.Uow, ops).SettleAsync(id, default);
        var pastDate = await new ReturnService(t.Uow, Customer).RequestAsync(
            new CreateReturnRequest(contractId, VietnamTime.Today.AddDays(-1), null, RefundMethod.Cash, null), default);

        requested.IsSuccess.Should().BeTrue(requested.Error?.Message);
        settled.IsSuccess.Should().BeTrue(settled.Error?.Message);
        settled.Value!.EarlyTerminationPenalty.Should().Be(200_000m);
        settled.Value.DamageDeductions.Should().Be(50_000m);
        settled.Value.RefundAmount.Should().Be(750_000m);
        (await t.Db.ReturnInspections.SingleAsync()).OtherDeduction.Should().Be(200_000m);
        pastDate.IsSuccess.Should().BeFalse("a return date in the past is refused");
    }
}
