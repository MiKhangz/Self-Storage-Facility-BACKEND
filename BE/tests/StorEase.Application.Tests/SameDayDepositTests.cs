using FluentAssertions;
using StorEase.Application.Features.Pricing;
using StorEase.Application.Features.Reservations;
using StorEase.Application.Features.Reservations.Dtos;
using StorEase.Application.Common;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using StorEase.Infrastructure.Services;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

public class SameDayDepositTests
{
    private static ReservationService As(TestDb t, Application.Interfaces.ICurrentUser user) => new(t.Uow, user, new FakeEmailService());

    private static void Price(TestDb t, decimal monthlyPrice)
    {
        t.Db.Add(new UnitPrice
        {
            PricingPolicy = new PricingPolicy { CreatedBy = StaffId, Version = 1, EffectiveFrom = VietnamTime.Today.AddYears(-1), Status = PricingPolicyStatus.Active, DepositMonths = 2 },
            UnitTypeId = SmallTypeId, BasePricePerMonth = monthlyPrice, Discount3Month = 10,
        });
        t.Db.SaveChanges();
    }

    private static async Task<ReservationDto> PaidBooking(TestDb t, decimal monthlyPrice = 900_000m)
    {
        Price(t, monthlyPrice);
        var booked = await As(t, Customer).CreateAsync(new CreateReservationRequest(FacilityId, SmallTypeId, VietnamTime.Today, 3, A101, SameDay: true), default);
        booked.IsSuccess.Should().BeTrue(booked.Error?.Message);
        var invoice = t.Db.Invoices.Single(i => i.ReservationId == booked.Value!.ReservationId);
        invoice.PaidAmount = invoice.TotalAmount;
        invoice.Status = InvoiceStatus.Paid;
        t.Db.SaveChanges();
        return booked.Value!;
    }

    [Theory]
    [InlineData(900000, 810000, 100000)]
    [InlineData(6000000, 5400000, 180000)]
    public async Task Deposit_has_a_minimum_after_plan_discount(decimal price, decimal rent, decimal deposit)
    {
        using var t = Create();
        Price(t, price);
        var terms = await RentCalculator.TermsAsync(t.Uow, FacilityId, SmallTypeId, VietnamTime.Today, 3, default, sameDay: true);
        terms.Value!.MonthlyRent.Should().Be(rent);
        terms.Value.Deposit.Should().Be(deposit);
        var later = await RentCalculator.TermsAsync(t.Uow, FacilityId, SmallTypeId, MoveIn, 3, default);
        later.Value!.Deposit.Should().Be(rent * 2);
    }

    [Theory]
    [InlineData(900000, 810000)]
    [InlineData(6000000, 5400000)]
    public async Task Customer_cancellation_within_3_hours_refunds_the_full_upfront_payment(decimal price, decimal fullRefund)
    {
        using var t = Create();
        var booked = await PaidBooking(t, price);
        var cancelled = await As(t, Customer).CancelAsync(booked.ReservationId, new(null), default);
        cancelled.IsSuccess.Should().BeTrue();
        cancelled.Value!.DepositRefundable.Should().BeTrue();
        t.Db.DepositRefunds.AsEnumerable().Sum(r => r.Amount).Should().Be(fullRefund);
        t.Db.StorageUnits.Find(A101)!.Status.Should().Be(StorageUnitStatus.Available);
    }

    [Theory]
    [InlineData(900000, 710000)]   // 810,000 rent -> 1/30 = 27,000 -> min 100,000 retained -> 710,000 refund
    [InlineData(6000000, 5220000)] // 5,400,000 rent -> 1/30 = 180,000 retained -> 5,220,000 refund
    [InlineData(100000, 0)]        // 90,000 rent -> min 100,000 retained -> 0 refund
    public async Task Customer_cancellation_after_3_hours_retains_one_thirtieth_of_monthly_rent_minimum_100k(decimal price, decimal refund)
    {
        using var t = Create();
        var booked = await PaidBooking(t, price);
        t.Db.Reservations.Find(booked.ReservationId)!.CreatedAt = DateTime.UtcNow.AddHours(-3).AddMinutes(-1);
        t.Db.SaveChanges();
        var cancelled = await As(t, Customer).CancelAsync(booked.ReservationId, new(null), default);
        cancelled.IsSuccess.Should().BeTrue();
        cancelled.Value!.DepositRefundable.Should().Be(refund > 0);
        t.Db.DepositRefunds.AsEnumerable().Sum(r => r.Amount).Should().Be(refund);
        t.Db.StorageUnits.Find(A101)!.Status.Should().Be(StorageUnitStatus.Available);
        if (refund > 0)
        {
            var repeated = await As(t, Customer).RequestRefundAsync(booked.ReservationId, default);
            repeated.Value!.DepositPaid.Should().Be(refund);
            t.Db.DepositRefunds.Should().ContainSingle();
        }
    }

    [Theory]
    [InlineData(900000, 710000)]   // 810,000 - 100,000
    [InlineData(6000000, 5220000)] // 5,400,000 - 180,000 (1/30 of 5,400,000)
    public async Task No_show_at_2030_refunds_discounted_month_minus_one_thirtieth_fund_once(decimal price, decimal expectedRefund)
    {
        using var t = Create();
        var booked = await PaidBooking(t, price);
        var day = VietnamTime.Today;
        var cutoff = ReservationService.SameDayNoShowAt(day);
        cutoff.Should().Be(VietnamTime.StartOf(day).AddHours(20.5));
        await As(t, Staff).RunDailyAsync(day, 10, default, cutoff.AddSeconds(-1));
        t.Db.Reservations.Find(booked.ReservationId)!.Status.Should().Be(ReservationStatus.Pending);
        await As(t, Staff).RunDailyAsync(day, 10, default, cutoff);
        t.Db.Reservations.Find(booked.ReservationId)!.Status.Should().Be(ReservationStatus.Expired);
        t.Db.DepositRefunds.Should().ContainSingle(r => r.Amount == expectedRefund);
        t.Db.StorageUnits.Find(A101)!.Status.Should().Be(StorageUnitStatus.Available);
        await As(t, Staff).RunDailyAsync(day, 10, default, cutoff.AddMinutes(1));
        t.Db.DepositRefunds.Should().ContainSingle();
    }

    [Fact]
    public async Task Facility_fault_still_refunds_the_entire_upfront_payment()
    {
        using var t = Create();
        var booked = await PaidBooking(t);
        t.Db.StorageUnits.Find(A101)!.Status = StorageUnitStatus.Maintenance;
        t.Db.SaveChanges();
        await As(t, Customer).CancelAsync(booked.ReservationId, new(null), default);
        t.Db.DepositRefunds.Should().ContainSingle(r => r.Amount == 810_000m);
    }

    [Theory]
    [InlineData(900000, 710000)]
    [InlineData(6000000, 5220000)]
    public async Task Refund_request_for_a_closed_booking_uses_the_one_thirtieth_fund_deduction(decimal price, decimal expectedRefund)
    {
        using var t = Create();
        var booked = await PaidBooking(t, price);
        t.Db.Reservations.Find(booked.ReservationId)!.Status = ReservationStatus.Expired;
        t.Db.SaveChanges();
        var refund = await As(t, Customer).RequestRefundAsync(booked.ReservationId, default);
        refund.Value!.DepositPaid.Should().Be(expectedRefund);
        t.Db.DepositRefunds.Should().ContainSingle(r => r.Amount == expectedRefund);
    }

    [Fact]
    public async Task Completed_move_in_is_not_expired_at_2030()
    {
        using var t = Create();
        var booked = await PaidBooking(t);
        t.Db.Reservations.Find(booked.ReservationId)!.Status = ReservationStatus.HandedOver;
        t.Db.StorageUnits.Find(A101)!.Status = StorageUnitStatus.Occupied;
        t.Db.SaveChanges();
        await As(t, Staff).RunDailyAsync(VietnamTime.Today, 10, default, ReservationService.SameDayNoShowAt(VietnamTime.Today));
        t.Db.Reservations.Find(booked.ReservationId)!.Status.Should().Be(ReservationStatus.HandedOver);
        t.Db.StorageUnits.Find(A101)!.Status.Should().Be(StorageUnitStatus.Occupied);
        t.Db.DepositRefunds.Should().BeEmpty();
    }
}
