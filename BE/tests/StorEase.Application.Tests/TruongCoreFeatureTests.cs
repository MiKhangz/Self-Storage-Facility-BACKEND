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
            new DateOnly(2026, 1, 1), null, 1, 3, 30, 2, 14, 60, 24), default);
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
        var service = new InvoiceService(db.Uow, new FakeCurrentUser(11, Roles.StorageCustomer));

        var paid = await service.PayOnlineAsync(10, new PaymentRequest(500_000, "GW-1"), default);
        var duplicate = await service.PayOnlineAsync(10, new PaymentRequest(1, "GW-2"), default);
        var invoice = await db.Db.Invoices.Include(i => i.Payments).SingleAsync(i => i.InvoiceId == 10);

        paid.IsSuccess.Should().BeTrue();
        paid.Value.Status.Should().Be(InvoiceStatus.Paid);
        paid.Value.OutstandingAmount.Should().Be(0);
        duplicate.IsSuccess.Should().BeFalse();
        duplicate.Error!.Type.Should().Be(ErrorType.Conflict);
        invoice.PaidAmount.Should().Be(500_000m);
        invoice.Payments.Should().ContainSingle(p => p.Status == PaymentStatus.Succeeded);
    }

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
        var service = new InvoiceService(db.Uow, new FakeCurrentUser(9, Roles.BusinessOperationsManager));

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
            VietnamTime.Today.AddDays(30), null, 1, 3, 30, 2, 14, 60, 24), default);
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
        var service = new InvoiceService(db.Uow, new FakeCurrentUser(31, Roles.StorageCustomer));

        var result = await service.PayOnlineAsync(30, new PaymentRequest(50_000, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(InvoiceStatus.Overdue);
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
