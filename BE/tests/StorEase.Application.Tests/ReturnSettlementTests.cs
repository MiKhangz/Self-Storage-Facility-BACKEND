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
        var ops = new FakeCurrentUser(StaffId, Roles.BusinessOperationsManager);

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

    [Fact]
    public async Task Settling_pays_late_rent_from_the_deposit_and_leaves_no_code_working()
    {
        using var t = Create();
        var contractId = SeedActiveContract(t, earlyTerminationPercent: 0);
        var ops = new FakeCurrentUser(StaffId, Roles.BusinessOperationsManager);
        var id = (await new ReturnService(t.Uow, Customer).RequestAsync(
            new CreateReturnRequest(contractId, VietnamTime.Today.AddDays(1), null, RefundMethod.Cash, null), default)).Value!.UnitReturnId;
        // Late rent locked the customer out after the return was requested: an overdue invoice, its case, and a suspended (not expired) code.
        var invoice = new Invoice
        {
            ContractId = contractId, CustomerId = CustomerId, InvoiceNumber = "INV-LATE", IssueDate = VietnamTime.Today.AddDays(-20),
            DueDate = VietnamTime.Today.AddDays(-15), TotalAmount = 300_000m, Status = InvoiceStatus.Overdue,
        };
        t.Db.Add(invoice);
        t.Db.SaveChanges();
        t.Db.Add(new OverdueCase { ContractId = contractId, InvoiceId = invoice.InvoiceId, OverdueSince = VietnamTime.Today.AddDays(-14),
            Stage = OverdueStage.Suspended, AccessSuspendedAt = DateTime.UtcNow.AddDays(-1) });
        t.Db.Add(new AccessCode { ContractId = contractId, CodeHash = "x", IssuedAt = DateTime.UtcNow.AddDays(-30),
            ExpiresAt = DateTime.UtcNow.AddDays(60), IsActive = false, SuspendedAt = DateTime.UtcNow.AddDays(-1) });
        t.Db.SaveChanges();
        t.Db.ChangeTracker.Clear();

        await new ReturnService(t.Uow, Staff).ApproveAsync(id, new ApproveReturnRequest(DateTime.UtcNow.AddDays(1)), default);
        await new ReturnService(t.Uow, Staff).InspectAsync(id, new CreateReturnInspectionRequest(ConditionResult.Good, 0m, 0m, 0m), default);
        var settled = await new ReturnService(t.Uow, ops).SettleAsync(id, default);

        settled.Value!.RefundAmount.Should().Be(700_000m);
        t.Db.ChangeTracker.Clear();
        t.Db.Invoices.Single(i => i.InvoiceNumber == "INV-LATE").Status.Should().Be(InvoiceStatus.Paid, "the deposit paid it");
        t.Db.AccessCodes.Where(a => a.ContractId == contractId).Should().OnlyContain(a => !a.IsActive, "a closed rental opens nothing");
    }

    [Fact]
    public async Task Requesting_a_return_is_refused_when_the_contract_has_an_unpaid_invoice()
    {
        using var t = Create();
        var contractId = SeedActiveContract(t, earlyTerminationPercent: 0);
        t.Db.Add(new Invoice
        {
            ContractId = contractId, CustomerId = CustomerId, InvoiceNumber = "INV-UNPAID", IssueDate = VietnamTime.Today,
            DueDate = VietnamTime.Today.AddDays(5), TotalAmount = 900_000m, PaidAmount = 0m, Status = InvoiceStatus.Issued,
        });
        t.Db.SaveChanges();
        t.Db.ChangeTracker.Clear();

        var requested = await new ReturnService(t.Uow, Customer).RequestAsync(
            new CreateReturnRequest(contractId, VietnamTime.Today.AddDays(1), null, RefundMethod.Cash, null), default);

        requested.IsSuccess.Should().BeFalse();
        requested.Error!.Type.Should().Be(ErrorType.Conflict);
        requested.Error.Message.Should().Be("Please pay your outstanding invoices before requesting a return.");
    }

    [Fact]
    public async Task Settling_when_damages_exceed_deposit_automatically_issues_a_shortfall_invoice()
    {
        using var t = Create();
        var contractId = SeedActiveContract(t, earlyTerminationPercent: 0);
        var ops = new FakeCurrentUser(StaffId, Roles.BusinessOperationsManager);

        var id = (await new ReturnService(t.Uow, Customer).RequestAsync(
            new CreateReturnRequest(contractId, VietnamTime.Today.AddDays(1), null, RefundMethod.Cash, null), default)).Value!.UnitReturnId;
        await new ReturnService(t.Uow, Staff).ApproveAsync(id, new ApproveReturnRequest(DateTime.UtcNow.AddDays(1)), default);
        // Deposit is 1,000,000; damages are 200,000 cleaning + 1,250,000 repair = 1,450,000 -> 450,000 shortfall.
        await new ReturnService(t.Uow, Staff).InspectAsync(
            id, new CreateReturnInspectionRequest(ConditionResult.MajorDamage, 200_000m, 1_250_000m, 0m), default);
        var settled = await new ReturnService(t.Uow, ops).SettleAsync(id, default);

        settled.IsSuccess.Should().BeTrue(settled.Error?.Message);
        settled.Value!.RefundAmount.Should().Be(0m);
        settled.Value.Shortfall.Should().Be(450_000m);
        settled.Value.Status.Should().Be(ReturnStatus.Completed);
        settled.Value.ShortfallInvoiceId.Should().NotBeNull();
        settled.Value.ShortfallInvoiceNumber.Should().Be($"DMG-R{id}");

        t.Db.ChangeTracker.Clear();
        var shortfallInvoice = await t.Db.Invoices.Include(i => i.InvoiceItems)
            .SingleAsync(i => i.InvoiceId == settled.Value.ShortfallInvoiceId);
        shortfallInvoice.Status.Should().Be(InvoiceStatus.Issued);
        shortfallInvoice.TotalAmount.Should().Be(450_000m);
        shortfallInvoice.PaidAmount.Should().Be(0m);
        shortfallInvoice.InvoiceItems.Should().HaveCount(3);
    }

    [Fact]
    public async Task Settle_with_positive_refund_leads_to_AwaitingPayout_and_Pending_payout_record()
    {
        using var t = Create();
        var contractId = SeedActiveContract(t, earlyTerminationPercent: 0);
        var ops = new FakeCurrentUser(StaffId, Roles.BusinessOperationsManager);

        var id = (await new ReturnService(t.Uow, Customer).RequestAsync(
            new CreateReturnRequest(contractId, VietnamTime.Today.AddDays(1), null, RefundMethod.BankTransfer, "VCB 123456"), default)).Value!.UnitReturnId;
        await new ReturnService(t.Uow, Staff).ApproveAsync(id, new ApproveReturnRequest(DateTime.UtcNow.AddDays(1)), default);
        await new ReturnService(t.Uow, Staff).InspectAsync(id, new CreateReturnInspectionRequest(ConditionResult.Good, 100_000m, 0m, 0m), default);
        var settled = await new ReturnService(t.Uow, ops).SettleAsync(id, default);

        settled.IsSuccess.Should().BeTrue(settled.Error?.Message);
        settled.Value!.RefundAmount.Should().Be(900_000m);
        settled.Value.Status.Should().Be(ReturnStatus.AwaitingPayout);

        t.Db.ChangeTracker.Clear();
        var unitReturn = await t.Db.UnitReturns.SingleAsync(r => r.UnitReturnId == id);
        unitReturn.Status.Should().Be(ReturnStatus.AwaitingPayout);

        var payout = await t.Db.DepositRefunds.SingleAsync(d => d.UnitReturnId == id);
        payout.Amount.Should().Be(900_000m);
        payout.Status.Should().Be(DepositRefundStatus.Pending);
    }

    [Fact]
    public async Task Settle_with_zero_refund_creates_paid_payout_record_and_leads_to_Completed()
    {
        using var t = Create();
        var contractId = SeedActiveContract(t, earlyTerminationPercent: 0);
        var ops = new FakeCurrentUser(StaffId, Roles.BusinessOperationsManager);

        var id = (await new ReturnService(t.Uow, Customer).RequestAsync(
            new CreateReturnRequest(contractId, VietnamTime.Today.AddDays(1), null, RefundMethod.BankTransfer, "VCB 123456"), default)).Value!.UnitReturnId;
        await new ReturnService(t.Uow, Staff).ApproveAsync(id, new ApproveReturnRequest(DateTime.UtcNow.AddDays(1)), default);
        // Full 1,000,000 deposit consumed by deductions -> RefundAmount == 0
        await new ReturnService(t.Uow, Staff).InspectAsync(id, new CreateReturnInspectionRequest(ConditionResult.MinorDamage, 200_000m, 800_000m, 0m), default);
        var settled = await new ReturnService(t.Uow, ops).SettleAsync(id, default);

        settled.IsSuccess.Should().BeTrue(settled.Error?.Message);
        settled.Value!.RefundAmount.Should().Be(0m);
        settled.Value.Status.Should().Be(ReturnStatus.Completed);

        t.Db.ChangeTracker.Clear();
        var unitReturn = await t.Db.UnitReturns.SingleAsync(r => r.UnitReturnId == id);
        unitReturn.Status.Should().Be(ReturnStatus.Completed);

        var payout = await t.Db.DepositRefunds.SingleAsync(d => d.UnitReturnId == id);
        payout.Amount.Should().Be(0m);
        payout.Source.Should().Be(DepositRefundSource.Return);
        payout.Status.Should().Be(DepositRefundStatus.Paid);
        payout.PaidAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MarkPaid_on_an_AwaitingPayout_return_moves_it_to_Completed()
    {
        using var t = Create();
        var contractId = SeedActiveContract(t, earlyTerminationPercent: 0);
        var ops = new FakeCurrentUser(StaffId, Roles.BusinessOperationsManager);

        var id = (await new ReturnService(t.Uow, Customer).RequestAsync(
            new CreateReturnRequest(contractId, VietnamTime.Today.AddDays(1), null, RefundMethod.BankTransfer, "VCB 123456"), default)).Value!.UnitReturnId;
        await new ReturnService(t.Uow, Staff).ApproveAsync(id, new ApproveReturnRequest(DateTime.UtcNow.AddDays(1)), default);
        await new ReturnService(t.Uow, Staff).InspectAsync(id, new CreateReturnInspectionRequest(ConditionResult.Good, 0m, 0m, 0m), default);
        await new ReturnService(t.Uow, ops).SettleAsync(id, default);

        var payout = await t.Db.DepositRefunds.SingleAsync(d => d.UnitReturnId == id);
        var marked = await new StorEase.Application.Features.DepositRefunds.DepositRefundService(t.Uow, ops)
            .MarkPaidAsync(payout.DepositRefundId, new StorEase.Application.Features.DepositRefunds.MarkDepositRefundPaidRequest(RefundMethod.BankTransfer, "FT999888"), default);

        marked.IsSuccess.Should().BeTrue(marked.Error?.Message);
        marked.Value!.Status.Should().Be(DepositRefundStatus.Paid);

        t.Db.ChangeTracker.Clear();
        var unitReturn = await t.Db.UnitReturns.SingleAsync(r => r.UnitReturnId == id);
        unitReturn.Status.Should().Be(ReturnStatus.Completed);
    }

    [Fact]
    public async Task Return_cannot_move_to_Completed_while_its_DepositRefund_is_Pending()
    {
        using var t = Create();
        var contractId = SeedActiveContract(t, earlyTerminationPercent: 0);
        var ops = new FakeCurrentUser(StaffId, Roles.BusinessOperationsManager);

        var id = (await new ReturnService(t.Uow, Customer).RequestAsync(
            new CreateReturnRequest(contractId, VietnamTime.Today.AddDays(1), null, RefundMethod.BankTransfer, "VCB 123456"), default)).Value!.UnitReturnId;
        await new ReturnService(t.Uow, Staff).ApproveAsync(id, new ApproveReturnRequest(DateTime.UtcNow.AddDays(1)), default);
        await new ReturnService(t.Uow, Staff).InspectAsync(id, new CreateReturnInspectionRequest(ConditionResult.Good, 0m, 0m, 0m), default);
        var settled = await new ReturnService(t.Uow, ops).SettleAsync(id, default);
        var secondSettle = await new ReturnService(t.Uow, ops).SettleAsync(id, default);

        settled.Value!.Status.Should().Be(ReturnStatus.AwaitingPayout);
        secondSettle.IsSuccess.Should().BeFalse("an AwaitingPayout return cannot be re-settled");
        secondSettle.Error!.Type.Should().Be(ErrorType.Conflict);

        t.Db.ChangeTracker.Clear();
        var payout = await t.Db.DepositRefunds.SingleAsync(d => d.UnitReturnId == id);
        payout.Status.Should().Be(DepositRefundStatus.Pending);
        (await t.Db.UnitReturns.SingleAsync(r => r.UnitReturnId == id)).Status.Should().NotBe(ReturnStatus.Completed);
    }
}
