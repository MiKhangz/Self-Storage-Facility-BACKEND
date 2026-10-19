using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Invoices;
using StorEase.Application.Features.Invoices.Dtos;
using StorEase.Application.Features.Pricing;
using StorEase.Application.Features.Pricing.Dtos;
using StorEase.Application.Features.StorageUnits;
using StorEase.Application.Features.StorageUnits.Dtos;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Tests;

public sealed class TruongCoreFeatureTests
{
    [Fact]
    public async Task Quote_uses_the_effective_policy_discount_deposit_and_fees()
    {
        using var db = new TestDb();
        SeedRoleAndUser(db, 1);
        var facility = new Facility { FacilityId = 1, Code = "F1", Name = "Main", Address = "A", District = "D", Status = FacilityStatus.Active };
        var floor = new Floor { FloorId = 1, FacilityId = 1, FloorNumber = 1, Name = "Ground" };
        var type = new UnitType { UnitTypeId = 1, Code = "S", Name = "Small", AreaM2 = 4, Width = 2, Depth = 2, Height = 2 };
        var policy = new PricingPolicy
        {
            PricingPolicyId = 1, CreatedBy = 1, Version = 1, EffectiveFrom = new DateOnly(2026, 1, 1),
            DepositMonths = 2, Status = PricingPolicyStatus.Active,
        };
        db.Db.AddRange(facility, floor, type, policy,
            new StorageUnit { StorageUnitId = 1, FacilityId = 1, FloorId = 1, UnitTypeId = 1, UnitCode = "A1", Status = StorageUnitStatus.Available },
            new UnitPrice { UnitPriceId = 1, PricingPolicyId = 1, UnitTypeId = 1, BasePricePerMonth = 1_000_000, Discount3Month = 10 },
            new FeeType { FeeTypeId = 1, PricingPolicyId = 1, Code = "SETUP", Name = "Setup", Amount = 5, IsPercentage = true });
        await db.Db.SaveChangesAsync();

        var service = new UnitCatalogService(db.Uow);
        var result = await service.QuoteAsync(1, 1, new DateOnly(2026, 10, 1), 3, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.MonthlyRent.Should().Be(900_000m);
        result.Value.RentSubtotal.Should().Be(2_700_000m);
        result.Value.DepositAmount.Should().Be(1_800_000m);
        result.Value.Fees.Single().Amount.Should().Be(135_000m);
        result.Value.TotalAmount.Should().Be(4_635_000m);
    }

    [Fact]
    public async Task Pricing_policy_can_be_created_priced_and_activated_without_schema_changes()
    {
        using var db = new TestDb();
        SeedRoleAndUser(db, 7);
        db.Db.UnitTypes.Add(new UnitType { UnitTypeId = 3, Code = "M", Name = "Medium", AreaM2 = 8, Width = 2, Depth = 4, Height = 2 });
        await db.Db.SaveChangesAsync();
        var service = new PricingService(db.Uow, new FakeCurrentUser(7, Roles.BusinessOperationsManager));

        var created = await service.CreatePolicyAsync(new CreatePricingPolicyRequest(
            new DateOnly(2026, 1, 1), null, 1, 3, 30, 2, 0, 14, 60, 24), default);
        created.IsSuccess.Should().BeTrue();
        var price = await service.UpsertUnitPriceAsync(created.Value.PricingPolicyId, 3,
            new UpsertUnitPriceRequest(2_000_000, 5, 10, 15), default);
        price.IsSuccess.Should().BeTrue();
        var active = await service.ActivatePolicyAsync(created.Value.PricingPolicyId, default);

        active.IsSuccess.Should().BeTrue();
        active.Value.Status.Should().Be(PricingPolicyStatus.Active);
        active.Value.UnitPrices.Single().BasePricePerMonth.Should().Be(2_000_000m);
    }

    [Fact]
    public async Task Customer_can_pay_only_its_own_invoice_and_invoice_is_marked_paid_atomically()
    {
        using var db = new TestDb();
        SeedRoleAndUser(db, 11);
        db.Db.Invoices.Add(new Invoice
        {
            InvoiceId = 10, CustomerId = 11, InvoiceNumber = "INV-10", IssueDate = new DateOnly(2026, 10, 1),
            DueDate = new DateOnly(2026, 10, 10), TotalAmount = 500_000, Status = InvoiceStatus.Issued,
        });
        await db.Db.SaveChangesAsync();
        var service = new InvoiceService(db.Uow, new FakeCurrentUser(11, Roles.StorageCustomer), Gateway);

        var intent = await service.PayOnlineAsync(10, new PaymentRequest(500_000, "GW-1"), default);
        var pending = await db.Db.Invoices.AsNoTracking().SingleAsync(i => i.InvoiceId == 10);
        var paid = await service.ConfirmGatewayAsync(Callback(intent.Value!.PaymentCode, PaymentStatus.Succeeded, 500_000), default);
        var duplicate = await service.PayOnlineAsync(10, new PaymentRequest(1, "GW-2"), default);
        var otherCustomer = await new InvoiceService(db.Uow, new FakeCurrentUser(99, Roles.StorageCustomer), Gateway)
            .PayOnlineAsync(10, new PaymentRequest(1, null), default);
        var invoice = await db.Db.Invoices.Include(i => i.Payments).SingleAsync(i => i.InvoiceId == 10);

        intent.Value.Status.Should().Be(PaymentStatus.Pending);
        pending.PaidAmount.Should().Be(0, "nothing is credited until the gateway confirms");
        paid.IsSuccess.Should().BeTrue(paid.Error?.Message);
        paid.Value!.Status.Should().Be(InvoiceStatus.Paid);
        paid.Value.OutstandingAmount.Should().Be(0);
        duplicate.Error!.Type.Should().Be(ErrorType.Conflict);
        otherCustomer.Error!.Type.Should().Be(ErrorType.NotFound);
        invoice.PaidAmount.Should().Be(500_000m);
        invoice.Payments.Should().ContainSingle(p => p.Status == PaymentStatus.Succeeded);
    }

    [Fact]
    public async Task Gateway_callback_with_a_bad_signature_or_wrong_amount_credits_nothing()
    {
        using var db = new TestDb();
        SeedRoleAndUser(db, 11);
        db.Db.Invoices.Add(new Invoice
        {
            InvoiceId = 10, CustomerId = 11, InvoiceNumber = "INV-10", IssueDate = new DateOnly(2026, 10, 1),
            DueDate = new DateOnly(2026, 10, 10), TotalAmount = 500_000, Status = InvoiceStatus.Issued,
        });
        await db.Db.SaveChangesAsync();
        var service = new InvoiceService(db.Uow, new FakeCurrentUser(11, Roles.StorageCustomer), Gateway);
        var code = (await service.PayOnlineAsync(10, new PaymentRequest(500_000, null), default)).Value!.PaymentCode;

        var forged = await service.ConfirmGatewayAsync(Callback(code, PaymentStatus.Succeeded, 500_000) with { Signature = new string('0', 64) }, default);
        var wrongAmount = await service.ConfirmGatewayAsync(Callback(code, PaymentStatus.Succeeded, 1), default);
        var failed = await service.ConfirmGatewayAsync(Callback(code, PaymentStatus.Failed, 500_000), default);
        var replayed = await service.ConfirmGatewayAsync(Callback(code, PaymentStatus.Failed, 500_000), default);

        forged.Error!.Type.Should().Be(ErrorType.Unauthorized);
        wrongAmount.Error!.Type.Should().Be(ErrorType.Conflict);
        failed.IsSuccess.Should().BeTrue();
        replayed.IsSuccess.Should().BeTrue("the gateway retries callbacks");
        (await db.Db.Invoices.SingleAsync(i => i.InvoiceId == 10)).PaidAmount.Should().Be(0);
    }

    [Fact]
    public async Task Refunding_a_payment_reopens_the_invoice()
    {
        using var db = new TestDb();
        SeedRoleAndUser(db, 21);
        db.Db.Invoices.Add(new Invoice
        {
            InvoiceId = 20, CustomerId = 21, InvoiceNumber = "INV-20", IssueDate = VietnamTime.Today,
            DueDate = VietnamTime.Today.AddDays(5), TotalAmount = 100_000, Status = InvoiceStatus.Issued,
        });
        db.Db.InvoiceItems.Add(new InvoiceItem { InvoiceId = 20, Description = "Rent", Quantity = 1, UnitAmount = 100_000, LineAmount = 100_000 });
        await db.Db.SaveChangesAsync();
        var bom = new InvoiceService(db.Uow, new FakeCurrentUser(21, Roles.BusinessOperationsManager), Gateway);
        var paid = await bom.RecordManualPaymentAsync(20, new PaymentRequest(100_000, "CASH"), default);

        var refunded = await bom.RefundPaymentAsync(paid.Value!.Payments.Single().PaymentId, new RefundRequest("Paid twice"), default);
        var again = await bom.RefundPaymentAsync(paid.Value.Payments.Single().PaymentId, new RefundRequest(null), default);

        refunded.Value!.Status.Should().Be(InvoiceStatus.Issued);
        refunded.Value.OutstandingAmount.Should().Be(100_000m);
        refunded.Value.Payments.Single().Status.Should().Be(PaymentStatus.Refunded);
        again.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    private static readonly PaymentGatewayOptions Gateway = new() { Secret = "test-secret", CheckoutBaseUrl = "http://pay.test" };

    private static GatewayCallbackRequest Callback(string code, PaymentStatus status, decimal amount) =>
        new(code, status, amount, "REF-1", InvoiceService.Sign(Gateway.Secret, code, status, amount, "REF-1"));

    [Fact]
    public async Task Manual_payment_cannot_exceed_invoice_balance()
    {
        using var db = new TestDb();
        SeedRoleAndUser(db, 21);
        db.Db.Invoices.Add(new Invoice
        {
            InvoiceId = 20, CustomerId = 21, InvoiceNumber = "INV-20", IssueDate = new DateOnly(2026, 10, 1),
            DueDate = new DateOnly(2026, 10, 10), TotalAmount = 100_000, PaidAmount = 25_000, Status = InvoiceStatus.Issued,
        });
        await db.Db.SaveChangesAsync();
        var service = new InvoiceService(db.Uow, new FakeCurrentUser(9, Roles.BusinessOperationsManager), Gateway);

        var result = await service.RecordManualPaymentAsync(20, new PaymentRequest(75_001, "CASH-1"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        (await db.Db.Payments.CountAsync()).Should().Be(0);
        (await db.Db.Invoices.Where(i => i.InvoiceId == 20).Select(i => i.PaidAmount).SingleAsync()).Should().Be(25_000m);
    }

    [Fact]
    public async Task Future_pricing_policy_cannot_be_activated_before_it_starts()
    {
        using var db = new TestDb();
        SeedRoleAndUser(db, 7);
        db.Db.UnitTypes.Add(new UnitType { UnitTypeId = 3, Code = "M", Name = "Medium", AreaM2 = 8, Width = 2, Depth = 4, Height = 2 });
        await db.Db.SaveChangesAsync();
        var service = new PricingService(db.Uow, new FakeCurrentUser(7, Roles.BusinessOperationsManager));
        var created = await service.CreatePolicyAsync(new CreatePricingPolicyRequest(
            VietnamTime.Today.AddDays(30), null, 1, 3, 30, 2, 0, 14, 60, 24), default);
        await service.UpsertUnitPriceAsync(created.Value.PricingPolicyId, 3, new UpsertUnitPriceRequest(2_000_000, 5, 10, 15), default);

        var result = await service.ActivatePolicyAsync(created.Value.PricingPolicyId, default);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Partial_payment_keeps_an_overdue_invoice_overdue()
    {
        using var db = new TestDb();
        SeedRoleAndUser(db, 31);
        db.Db.Invoices.Add(new Invoice
        {
            InvoiceId = 30, CustomerId = 31, InvoiceNumber = "INV-30", IssueDate = new DateOnly(2026, 9, 1),
            DueDate = new DateOnly(2026, 9, 10), TotalAmount = 100_000, Status = InvoiceStatus.Overdue,
        });
        await db.Db.SaveChangesAsync();
        var service = new InvoiceService(db.Uow, new FakeCurrentUser(31, Roles.StorageCustomer), Gateway);
        var intent = await service.PayOnlineAsync(30, new PaymentRequest(50_000, null), default);

        var result = await service.ConfirmGatewayAsync(Callback(intent.Value!.PaymentCode, PaymentStatus.Succeeded, 50_000), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(InvoiceStatus.Overdue);
        result.Value.OutstandingAmount.Should().Be(50_000m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Quote_rejects_a_rental_length_below_one_month(int rentalMonths)
    {
        using var db = new TestDb();

        var result = await new UnitCatalogService(db.Uow).QuoteAsync(1, 1, new DateOnly(2026, 11, 1), rentalMonths, default);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Validation);
    }

    private static void SeedRoleAndUser(TestDb db, int userId)
    {
        db.Db.Roles.Add(new Role { RoleId = 1, RoleName = "TestRole", Description = "Tests" });
        db.Db.Users.Add(new User
        {
            UserId = userId, RoleId = 1, FullName = "Test User", Email = $"test{userId}@example.com",
            PhoneNumber = "0900000000", PasswordHash = "hash", Status = UserStatus.Active, CreatedAt = DateTime.UtcNow,
        });
        db.Db.SaveChanges();
    }
}
