using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.DepositRefunds;
using StorEase.Application.Features.Returns;
using StorEase.Application.Features.Returns.Dtos;
using StorEase.Application.Features.Reservations;
using StorEase.Application.Features.Reservations.Dtos;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

/// <summary>Every deposit StorEase owes back is tracked until Business Operations mark it paid.</summary>
public class DepositRefundTests
{
    // A seeded user, because PaidBy is a real foreign key to User.
    private static readonly FakeCurrentUser Ops = new(StaffId, Roles.BusinessOperationsManager);

    [Fact]
    public async Task A_settled_handover_owes_its_refund_until_ops_mark_it_paid_once()
    {
        using var t = Create();
        var today = VietnamTime.Today;
        var contract = new Contract
        {
            Reservation = new Reservation
            {
                CustomerId = CustomerId, FacilityId = FacilityId, UnitTypeId = SmallTypeId, StorageUnitId = A101, ReservationCode = "RS-REFUND",
                MoveInDate = today.AddDays(-30), RentalMonths = 3, Status = ReservationStatus.HandedOver, CreatedAt = DateTime.UtcNow,
            },
            CustomerId = CustomerId, StorageUnitId = A101, ContractNumber = "CT-REFUND", StartDate = today.AddDays(-30),
            EndDate = today.AddDays(60), MonthlyRent = 900_000m, DepositAmount = 1_000_000m, PaymentDay = 10,
            SignedAt = DateTime.UtcNow, Status = ContractStatus.Active,
        };
        t.Db.Add(contract);
        t.Db.SaveChanges();
        var id = (await new ReturnService(t.Uow, Customer).RequestAsync(
            new CreateReturnRequest(contract.ContractId, today.AddDays(1), null, RefundMethod.BankTransfer, "VCB 0123456789"), default)).Value!.UnitReturnId;
        await new ReturnService(t.Uow, Staff).ApproveAsync(id, new ApproveReturnRequest(DateTime.UtcNow.AddDays(1)), default);
        await new ReturnService(t.Uow, Staff).InspectAsync(id, new CreateReturnInspectionRequest(ConditionResult.Good, 100_000m, 0m, 0m), default);
        await new ReturnService(t.Uow, Ops).SettleAsync(id, default);

        var owed = (await new DepositRefundService(t.Uow, Customer).ListAsync(null, 1, 20, default)).Value!.Items.Single();
        var paid = await new DepositRefundService(t.Uow, Ops).MarkPaidAsync(owed.DepositRefundId,
            new MarkDepositRefundPaidRequest(RefundMethod.BankTransfer, "FT26102200001"), default);
        var again = await new DepositRefundService(t.Uow, Ops).MarkPaidAsync(owed.DepositRefundId,
            new MarkDepositRefundPaidRequest(RefundMethod.Cash, null), default);
        var otherManager = await new DepositRefundService(t.Uow, new FakeCurrentUser(77, Roles.FacilityManager)).ListAsync(null, 1, 20, default);

        owed.Source.Should().Be(DepositRefundSource.Return);
        owed.Amount.Should().Be(900_000m, "the deposit less 100,000 cleaning");
        owed.Status.Should().Be(DepositRefundStatus.Pending);
        owed.RefundAccount.Should().Be("VCB 0123456789", "the customer's choice comes with it");
        paid.Value!.Status.Should().Be(DepositRefundStatus.Paid);
        paid.Value.ReferenceNo.Should().Be("FT26102200001");
        again.Error!.Type.Should().Be(ErrorType.Conflict, "a refund is paid once");
        otherManager.Value!.TotalCount.Should().Be(0, "manager 77 works at no facility");
    }

    [Fact]
    public async Task Staff_cancelling_a_booking_with_a_paid_deposit_owes_it_back()
    {
        using var t = Create();
        var reservation = new Reservation
        {
            CustomerId = CustomerId, FacilityId = FacilityId, UnitTypeId = SmallTypeId, ReservationCode = "RS-CANCEL",
            MoveInDate = VietnamTime.Today.AddDays(5), RentalMonths = 1, Status = ReservationStatus.Pending, CreatedAt = DateTime.UtcNow.AddDays(-3),
        };
        t.Db.Add(reservation);
        t.Db.SaveChanges();
        t.Db.Add(new Invoice
        {
            ReservationId = reservation.ReservationId, CustomerId = CustomerId, InvoiceNumber = "DEP-CANCEL",
            IssueDate = VietnamTime.Today, DueDate = VietnamTime.Today, TotalAmount = 900_000m, PaidAmount = 900_000m, Status = InvoiceStatus.Paid,
        });
        t.Db.SaveChanges();

        var cancelled = await new ReservationService(t.Uow, Staff, new FakeEmailService())
            .CancelAsync(reservation.ReservationId, new CancelReservationRequest("Khách đổi ý"), default);

        cancelled.Value!.DepositRefundable.Should().BeTrue();
        var owed = await t.Db.DepositRefunds.SingleAsync();
        owed.Source.Should().Be(DepositRefundSource.Cancellation);
        owed.Amount.Should().Be(900_000m);
        owed.Method.Should().BeNull("staff choose how to pay it");
    }

    [Fact]
    public void A_bank_transfer_needs_its_reference()
    {
        var validator = new MarkDepositRefundPaidRequestValidator();

        validator.Validate(new MarkDepositRefundPaidRequest(RefundMethod.BankTransfer, null)).IsValid.Should().BeFalse();
        validator.Validate(new MarkDepositRefundPaidRequest(RefundMethod.Cash, null)).IsValid.Should().BeTrue();
    }
}
