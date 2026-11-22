using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.DepositRefunds;
using StorEase.Application.Features.Invoices;
using StorEase.Application.Features.Invoices.Dtos;
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

    /// <summary>A booking whose 900,000 deposit invoice was paid once for each of <paramref name="payments"/>.</summary>
    private static (Reservation Booking, List<int> PaymentIds) PaidDeposit(TestDb t, ReservationStatus status, int payments = 1)
    {
        var booking = new Reservation
        {
            CustomerId = CustomerId, FacilityId = FacilityId, UnitTypeId = SmallTypeId, StorageUnitId = A101, ReservationCode = "RS-TWICE",
            MoveInDate = VietnamTime.Today.AddDays(5), RentalMonths = 1, Status = status, CreatedAt = DateTime.UtcNow,
        };
        t.Db.Add(booking);
        t.Db.SaveChanges();
        var invoice = new Invoice
        {
            ReservationId = booking.ReservationId, CustomerId = CustomerId, InvoiceNumber = "DEP-TWICE", IssueDate = VietnamTime.Today,
            DueDate = booking.MoveInDate, TotalAmount = 900_000m, PaidAmount = 900_000m * payments, Status = InvoiceStatus.Paid,
        };
        invoice.InvoiceItems.Add(InvoiceBook.Line(InvoiceBook.DepositDescription, 900_000m));
        for (var i = 0; i < payments; i++)
            invoice.Payments.Add(new Payment
            {
                PaymentCode = $"PY-TWICE{i}", Method = PaymentMethod.Gateway, Amount = 900_000m, PaidAt = DateTime.UtcNow, Status = PaymentStatus.Succeeded,
            });
        t.Db.Add(invoice);
        t.Db.SaveChanges();
        return (booking, invoice.Payments.Select(p => p.PaymentId).ToList());
    }

    private static Task<Result<InvoiceDto>> Refund(TestDb t, int paymentId) =>
        new InvoiceService(t.Uow, Ops, new PaymentGatewayOptions()).RefundPaymentAsync(paymentId, new RefundRequest(null), default);

    [Theory]
    [InlineData(ReservationStatus.Pending)]
    [InlineData(ReservationStatus.Confirmed)]
    [InlineData(ReservationStatus.HandedOver)]
    public async Task A_deposit_is_not_refunded_as_a_payment_but_an_overpayment_is(ReservationStatus status)
    {
        using var t = Create();
        var (booking, ids) = PaidDeposit(t, status, payments: 2);
        if (status == ReservationStatus.HandedOver)
        {
            t.Db.Add(new Contract
            {
                ReservationId = booking.ReservationId, CustomerId = CustomerId, StorageUnitId = A101, ContractNumber = "CT-TWICE",
                StartDate = booking.MoveInDate, EndDate = booking.MoveInDate.AddMonths(1), PaymentDay = 5, DepositAmount = 900_000m,
                Status = ContractStatus.Active,
            });
            t.Db.SaveChanges();
        }

        var extra = await Refund(t, ids[1]);
        var deposit = await Refund(t, ids[0]);

        extra.IsSuccess.Should().BeTrue("the customer paid twice");
        extra.Value!.Status.Should().Be(InvoiceStatus.Paid);
        deposit.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task A_cancelled_deposit_is_paid_back_only_once()
    {
        using var t = Create();
        var (booking, ids) = PaidDeposit(t, ReservationStatus.Confirmed, payments: 2);
        await new ReservationService(t.Uow, Staff, new FakeEmailService())
            .CancelAsync(booking.ReservationId, new CancelReservationRequest("Khách đổi ý"), default);

        var refunded = await Refund(t, ids[1]);

        (await t.Db.DepositRefunds.SingleAsync()).Amount.Should().Be(1_800_000m, "Deposit Refunds already owes both payments");
        refunded.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Money_that_arrives_after_a_cancellation_can_still_be_refunded()
    {
        using var t = Create();
        var (_, ids) = PaidDeposit(t, ReservationStatus.Cancelled);

        var refunded = await Refund(t, ids[0]);

        refunded.Value!.Status.Should().Be(InvoiceStatus.WrittenOff, "nothing else would pay it back, and the booking stays closed");
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task A_terminated_rentals_deposit_is_refunded_as_a_payment_only_when_nothing_can_settle_it(bool returnOpen, bool allowed)
    {
        using var t = Create();
        var (booking, ids) = PaidDeposit(t, ReservationStatus.HandedOver);
        var contract = new Contract
        {
            ReservationId = booking.ReservationId, CustomerId = CustomerId, StorageUnitId = A101, ContractNumber = "CT-ENDED",
            StartDate = booking.MoveInDate, EndDate = booking.MoveInDate.AddMonths(1), PaymentDay = 5, DepositAmount = 900_000m,
            Status = ContractStatus.Terminated,
        };
        t.Db.Add(contract);
        if (returnOpen)
            t.Db.Add(new UnitReturn
            {
                Contract = contract, RequestedAt = DateTime.UtcNow, PlannedReturnDate = VietnamTime.Today,
                MoveOutDeadline = VietnamTime.Today, Status = ReturnStatus.Inspected,
            });
        t.Db.SaveChanges();

        (await Refund(t, ids[0])).IsSuccess.Should().Be(allowed);
    }

    [Theory]
    [InlineData(ReservationStatus.Cancelled, false, false)]
    [InlineData(ReservationStatus.Expired, false, false)]
    [InlineData(ReservationStatus.Cancelled, true, false)] // the cancellation's own refund was already paid out
    [InlineData(ReservationStatus.Cancelled, true, true)] // cash at the desk
    public async Task Money_that_lands_on_a_closed_booking_is_owed_back_once(ReservationStatus status, bool refundedBefore, bool cash)
    {
        using var t = Create();
        var booking = new Reservation
        {
            CustomerId = CustomerId, FacilityId = FacilityId, UnitTypeId = SmallTypeId, ReservationCode = "RS-LATE",
            MoveInDate = VietnamTime.Today.AddDays(5), RentalMonths = 1, Status = status, CreatedAt = DateTime.UtcNow,
        };
        t.Db.Add(booking);
        t.Db.SaveChanges();
        if (refundedBefore)
            t.Db.Add(new DepositRefund
            {
                CustomerId = CustomerId, FacilityId = FacilityId, ReservationId = booking.ReservationId, Amount = 100_000m,
                Source = DepositRefundSource.Cancellation, Status = DepositRefundStatus.Paid, CreatedAt = DateTime.UtcNow,
            });
        var invoice = new Invoice
        {
            ReservationId = booking.ReservationId, CustomerId = CustomerId, InvoiceNumber = "DEP-LATE", IssueDate = VietnamTime.Today,
            DueDate = booking.MoveInDate, TotalAmount = 900_000m, PaidAmount = refundedBefore ? 100_000m : 0m,
            Status = refundedBefore ? InvoiceStatus.Issued : InvoiceStatus.WrittenOff,
        };
        var late = new Payment { PaymentCode = "PY-LATE", Method = PaymentMethod.Gateway, Amount = 800_000m, Status = PaymentStatus.Pending };
        invoice.Payments.Add(late);
        t.Db.Add(invoice);
        t.Db.SaveChanges();
        t.Db.ChangeTracker.Clear();
        var invoices = new InvoiceService(t.Uow, cash ? Staff : Customer, new PaymentGatewayOptions { AllowSimulation = true });

        for (var i = 0; i < 2; i++)
        {
            if (cash)
                await invoices.RecordManualPaymentAsync(invoice.InvoiceId, new PaymentRequest(800_000m, null), default);
            else
                await invoices.SimulateGatewaySuccessAsync("PY-LATE", default);
        }

        t.Db.ChangeTracker.Clear();
        var owed = t.Db.DepositRefunds.Single(r => r.Status == DepositRefundStatus.Pending);
        owed.Source.Should().Be(DepositRefundSource.LatePayment);
        owed.Amount.Should().Be(800_000m);
        var paymentId = t.Db.Payments.Single(p => p.Status == PaymentStatus.Succeeded).PaymentId;
        (await Refund(t, paymentId)).Error!.Type.Should().Be(ErrorType.Conflict, "Deposit Refunds pays it back");
    }

    [Fact]
    public async Task A_deduction_from_the_deposit_is_not_refunded_as_a_payment()
    {
        using var t = Create();
        var invoice = new Invoice
        {
            CustomerId = CustomerId, InvoiceNumber = "INV-DPC", IssueDate = VietnamTime.Today, DueDate = VietnamTime.Today,
            TotalAmount = 100_000m, PaidAmount = 100_000m, Status = InvoiceStatus.Paid,
        };
        var deduction = new Payment { PaymentCode = "DPC1", Method = PaymentMethod.Deposit, Amount = 100_000m, Status = PaymentStatus.Succeeded };
        invoice.Payments.Add(deduction);
        t.Db.Add(invoice);
        t.Db.SaveChanges();

        (await Refund(t, deduction.PaymentId)).Error!.Type.Should().Be(ErrorType.Conflict);
    }
}
