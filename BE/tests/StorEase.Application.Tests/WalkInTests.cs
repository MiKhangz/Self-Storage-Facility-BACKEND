using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Invoices;
using StorEase.Application.Features.Reservations;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using StorEase.Infrastructure.Services;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

/// <summary>Walk-in booking: Confirmed with a unit at once, deposit paid in cash, new customers created at the desk.</summary>
public class WalkInTests
{
    private static WalkInService Service(TestDb t, Interfaces.ICurrentUser user)
    {
        var gateway = new PaymentGatewayOptions();
        return new WalkInService(t.Uow, user, new Pbkdf2PasswordHasher(), new InvoiceService(t.Uow, user, gateway),
            new ReservationService(t.Uow, user, new FakeEmailService()));
    }

    private static void SeedPolicy(TestDb t)
    {
        t.Db.Add(new UnitPrice
        {
            PricingPolicy = new PricingPolicy
            {
                CreatedBy = StaffId, Version = 1, EffectiveFrom = new DateOnly(2025, 1, 1), DepositMonths = 1, GraceDays = 5,
                NoticeDays = 30, LateFeePercentPerWeek = 2, SuspendAfterDays = 14, ClearOutAfterDays = 60, CancelFreeHours = 24,
                Status = PricingPolicyStatus.Active,
            },
            UnitTypeId = SmallTypeId, BasePricePerMonth = 900_000m,
        });
        t.Db.SaveChanges();
    }

    [Fact]
    public async Task A_new_customer_walks_in_and_leaves_with_a_confirmed_unit_and_a_paid_deposit()
    {
        using var t = Create();
        SeedPolicy(t);

        var result = await Service(t, Staff).BookAsync(new WalkInReservationRequest(
            null, new WalkInCustomerRequest("Khách Tại Quầy", "Desk@X.vn", "0901234567", "079203001234"),
            FacilityId, SmallTypeId, A101, VietnamTime.Today, 3), default);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value!.Status.Should().Be(ReservationStatus.Confirmed);
        result.Value.UnitCode.Should().Be("A-101");
        result.Value.DepositStatus.Should().Be(InvoiceStatus.Paid);
        var customer = await t.Db.Users.SingleAsync(u => u.Email == "desk@x.vn");
        customer.Status.Should().Be(UserStatus.Active);
        (await t.Db.Reservations.SingleAsync()).CreatedBy.Should().Be(StaffId);
        (await t.Db.StorageUnits.SingleAsync(u => u.StorageUnitId == A101)).Status.Should().Be(StorageUnitStatus.Reserved);
        (await t.Db.Payments.SingleAsync()).Method.Should().Be(PaymentMethod.Cash);
    }

    [Fact]
    public async Task Walk_in_refuses_a_taken_unit_another_facility_and_an_email_that_already_exists()
    {
        using var t = Create();
        SeedPolicy(t);
        var staff = Service(t, Staff);

        var existing = await staff.BookAsync(new WalkInReservationRequest(CustomerId, null, FacilityId, SmallTypeId, A101, VietnamTime.Today, 1), default);
        var sameUnit = await staff.BookAsync(new WalkInReservationRequest(CustomerId, null, FacilityId, SmallTypeId, A101, VietnamTime.Today, 1), default);
        var duplicateEmail = await staff.BookAsync(new WalkInReservationRequest(
            null, new WalkInCustomerRequest("Trùng", "customer@x.vn", "0901234567", null), FacilityId, SmallTypeId, A102, VietnamTime.Today, 1), default);
        var outsider = await Service(t, new FakeCurrentUser(77, Roles.FacilityStaff))
            .BookAsync(new WalkInReservationRequest(CustomerId, null, FacilityId, SmallTypeId, A102, VietnamTime.Today, 1), default);
        var found = await staff.SearchCustomersAsync("customer@", 1, 20, default);

        existing.IsSuccess.Should().BeTrue(existing.Error?.Message);
        sameUnit.Error!.Type.Should().Be(ErrorType.Conflict);
        duplicateEmail.Error!.Type.Should().Be(ErrorType.Conflict);
        outsider.Error!.Type.Should().Be(ErrorType.NotFound, "staff 77 works at no facility");
        found.Value!.Items.Should().ContainSingle(c => c.UserId == CustomerId);
        (await staff.GetCustomerAsync(CustomerId, default)).Value!.Email.Should().Be("customer@x.vn");
        (await staff.GetCustomerAsync(StaffId, default)).Error!.Type.Should().Be(ErrorType.NotFound, "staff accounts are not customers");
        (await t.Db.Users.CountAsync()).Should().Be(3, "the refused walk-in created no account");
    }
}
