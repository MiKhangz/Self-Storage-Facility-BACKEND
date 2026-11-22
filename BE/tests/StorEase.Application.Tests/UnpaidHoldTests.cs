using FluentAssertions;
using StorEase.Application.Common;
using StorEase.Application.Features.Reservations;
using StorEase.Application.Features.Reservations.Dtos;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

/// <summary>A booking holds its unit for 24 hours without its deposit.</summary>
public class UnpaidHoldTests
{
    [Theory]
    [InlineData(25, 0, true, ReservationStatus.Pending, true)]
    [InlineData(25, 0, true, ReservationStatus.Confirmed, true)] // staff already confirmed the unit
    [InlineData(23, 0, true, ReservationStatus.Pending, false)]
    [InlineData(25, 300_000, true, ReservationStatus.Pending, false)] // part paid: staff follow up
    [InlineData(25, 900_000, true, ReservationStatus.Pending, false)]
    [InlineData(25, 0, false, ReservationStatus.Pending, false)] // no deposit was due
    public async Task The_job_releases_a_unit_held_without_a_deposit_for_a_day(int hoursAgo, int paid, bool hasDeposit, ReservationStatus status, bool released)
    {
        using var t = Create();
        var booking = new Reservation
        {
            CustomerId = CustomerId, FacilityId = FacilityId, UnitTypeId = SmallTypeId, StorageUnitId = A101, ReservationCode = "RS-HOLD",
            MoveInDate = VietnamTime.Today.AddDays(20), RentalMonths = 1, Status = status, CreatedAt = DateTime.UtcNow.AddHours(-hoursAgo),
        };
        t.Db.Add(booking);
        t.Db.StorageUnits.Find(A101)!.Status = StorageUnitStatus.Reserved;
        t.Db.SaveChanges();
        if (hasDeposit)
            t.Db.Add(new Invoice
            {
                ReservationId = booking.ReservationId, CustomerId = CustomerId, InvoiceNumber = "DEP-HOLD", IssueDate = VietnamTime.Today,
                DueDate = booking.MoveInDate, TotalAmount = 900_000m, PaidAmount = paid,
                Status = paid == 900_000 ? InvoiceStatus.Paid : InvoiceStatus.Issued,
            });
        t.Db.SaveChanges();
        t.Db.ChangeTracker.Clear();

        var (expired, _) = await new ReservationService(t.Uow, new FakeCurrentUser(0, ""), new FakeEmailService())
            .RunDailyAsync(VietnamTime.Today, 10, default);

        t.Db.ChangeTracker.Clear();
        expired.Should().Be(0, "it is no no-show");
        t.Db.Reservations.Find(booking.ReservationId)!.Status.Should().Be(released ? ReservationStatus.Cancelled : status);
        t.Db.StorageUnits.Find(A101)!.Status.Should().Be(released ? StorageUnitStatus.Available : StorageUnitStatus.Reserved);
        if (!released)
            return;
        t.Db.Invoices.Single().Status.Should().Be(InvoiceStatus.WrittenOff);
        t.Db.DepositRefunds.Should().BeEmpty();
        var told = t.Db.Notifications.Single();
        told.Body.Should().Contain("24 giờ").And.NotContain("StorEase chưa có kho", "it was not our fault");
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(20, 1)]
    public async Task The_deposit_is_due_when_the_hold_ends_or_on_move_in_day_if_sooner(int moveInDays, int dueDays)
    {
        using var t = Create();
        t.Db.Add(new UnitPrice
        {
            PricingPolicy = new PricingPolicy
            {
                CreatedBy = StaffId, Version = 1, EffectiveFrom = new DateOnly(2025, 1, 1), DepositMonths = 2, Status = PricingPolicyStatus.Active,
            },
            UnitTypeId = SmallTypeId, BasePricePerMonth = 900_000m,
        });
        t.Db.SaveChanges();
        var today = VietnamTime.Today;

        var booked = await new ReservationService(t.Uow, Customer, new FakeEmailService())
            .CreateAsync(new CreateReservationRequest(FacilityId, SmallTypeId, today.AddDays(moveInDays), 1), default);

        booked.IsSuccess.Should().BeTrue(booked.Error?.Message);
        t.Db.Invoices.Single().DueDate.Should().Be(today.AddDays(dueDays));
    }
}
