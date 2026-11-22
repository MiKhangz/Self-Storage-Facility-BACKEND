using FluentAssertions;
using StorEase.Application.Common;
using StorEase.Application.Features.ActivityLogs;
using StorEase.Application.Features.Auth;
using StorEase.Application.Features.Auth.Dtos;
using StorEase.Application.Features.Billing;
using StorEase.Application.Features.Contracts;
using StorEase.Application.Features.Notifications;
using StorEase.Application.Features.Reservations;
using StorEase.Application.Features.Reservations.Dtos;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using StorEase.Infrastructure.Services;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

/// <summary>The bell gets every message the customer is emailed, except links that act as a password.</summary>
public class NotificationTests
{
    private static readonly DateOnly Today = VietnamTime.Today;

    private static List<Notification> Saved(TestDb t)
    {
        t.Db.ChangeTracker.Clear();
        return t.Db.Notifications.OrderBy(n => n.NotificationId).ToList();
    }

    private static int Rent(TestDb t, DateOnly end)
    {
        t.Db.Add(new PricingPolicy
        {
            CreatedBy = StaffId, Version = 1, EffectiveFrom = new DateOnly(2025, 1, 1), DepositMonths = 1, Status = PricingPolicyStatus.Active,
            EndReminderDays = 10, OverstayFeePercent = 150, MoveOutAfterDays = 7, CentralStorageKeepDays = 90,
        });
        var booking = new Reservation
        {
            CustomerId = CustomerId, FacilityId = FacilityId, UnitTypeId = SmallTypeId, StorageUnitId = A101, ReservationCode = "RS-BELL",
            MoveInDate = end.AddMonths(-3), RentalMonths = 3, Status = ReservationStatus.HandedOver, CreatedAt = DateTime.UtcNow,
        };
        t.Db.Add(booking);
        t.Db.SaveChanges();
        var contract = new Contract
        {
            ReservationId = booking.ReservationId, CustomerId = CustomerId, StorageUnitId = A101, ContractNumber = "CT-BELL",
            StartDate = end.AddMonths(-3), EndDate = end, MonthlyRent = 900_000m, DepositAmount = 900_000m, PaymentDay = 5,
            Status = ContractStatus.Active,
        };
        t.Db.Add(contract);
        t.Db.StorageUnits.Find(A101)!.Status = StorageUnitStatus.Occupied;
        t.Db.SaveChanges();
        t.Db.ChangeTracker.Clear();
        return contract.ContractId;
    }

    [Fact]
    public async Task A_booking_message_is_saved_even_when_the_email_fails()
    {
        using var t = Create();

        var booked = await new ReservationService(t.Uow, Customer, new FakeEmailService(delivers: false))
            .CreateAsync(new CreateReservationRequest(FacilityId, SmallTypeId, MoveIn, 3), default);

        var saved = Saved(t).Should().ContainSingle().Subject;
        saved.UserId.Should().Be(CustomerId);
        saved.Title.Should().Be("Đã nhận yêu cầu đặt kho");
        saved.Body.Should().Contain("A-101");
        saved.Link.Should().Be($"/booking/confirmed/{booked.Value!.ReservationId}");
    }

    [Fact]
    public async Task A_payment_reminder_reaches_the_bell_once_a_day_even_when_email_is_opted_out()
    {
        using var t = Create();
        var invoice = new Invoice
        {
            CustomerId = CustomerId, InvoiceNumber = "INV-BELL", IssueDate = Today, DueDate = Today.AddDays(3),
            TotalAmount = 500_000m, Status = InvoiceStatus.Issued,
        };
        t.Db.Add(invoice);
        t.Db.NotificationPreferences.Add(new NotificationPreference { UserId = CustomerId, EmailReminders = false, UpdatedAt = DateTime.UtcNow });
        t.Db.SaveChanges();
        var email = new FakeEmailService();
        var reminders = new ReminderService(t.Uow, Staff, email);

        await reminders.SendAsync(invoice.InvoiceId, ReminderTemplates.DueSoon, onlyIfOptedIn: true, default);
        await reminders.SendAsync(invoice.InvoiceId, ReminderTemplates.DueSoon, onlyIfOptedIn: true, default);

        email.Sent.Should().BeEmpty("the customer turned email reminders off");
        var saved = Saved(t).Should().ContainSingle().Subject;
        saved.Title.Should().Contain("INV-BELL");
        saved.Body.Should().Contain("500.000 VND", "a comma would read as decimals in Vietnamese");
        saved.Link.Should().Be("/payments");
    }

    [Fact]
    public async Task The_end_of_rental_reminder_reaches_the_bell_once()
    {
        using var t = Create();
        var id = Rent(t, Today.AddDays(10));
        var ends = new RentalEndService(t.Uow, Staff, new FakeEmailService(delivers: false));

        await ends.RunAsync(Today, default);
        await ends.RunAsync(Today, default);

        var saved = Saved(t).Should().ContainSingle().Subject;
        saved.Title.Should().Be("Kỳ thuê kho sắp kết thúc");
        saved.Link.Should().Be($"/my-storage/units/{id}");
    }

    [Fact]
    public async Task Moving_goods_to_the_warehouse_tells_the_last_day_to_collect()
    {
        using var t = Create();
        var id = Rent(t, Today.AddDays(-8));
        var ends = new RentalEndService(t.Uow, Staff, new FakeEmailService());
        await ends.RunAsync(Today, default);

        (await ends.MoveToCentralStorageAsync(id, default)).IsSuccess.Should().BeTrue();

        var saved = Saved(t).Should().ContainSingle().Subject;
        saved.Body.Should().Contain($"{Today.AddDays(90):dd/MM/yyyy}");
        saved.Link.Should().Be("/payments");
    }

    [Fact]
    public async Task A_password_reset_link_is_never_saved_as_a_notification()
    {
        using var t = Create();
        var email = new FakeEmailService();
        var jwt = new JwtService(new JwtOptions { Issuer = "StorEase", Audience = "StorEase.Frontend", Key = new string('k', 64), ExpiresMinutes = 60 });

        await new AuthService(t.Uow, new Pbkdf2PasswordHasher(), jwt, email, new AppOptions(), new ActivityLogService(t.Uow))
            .ForgotPasswordAsync(new ForgotPasswordRequest("customer@x.vn"), default);

        email.Sent.Should().ContainSingle();
        Saved(t).Should().BeEmpty();
    }

    [Fact]
    public async Task A_customer_lists_and_reads_only_their_own_and_only_up_to_what_they_were_shown()
    {
        using var t = Create();
        Notification New(int user, string title) => new() { UserId = user, Title = title, Body = "b", Link = "/payments", CreatedAt = DateTime.UtcNow };
        t.Db.Add(New(CustomerId, "first"));
        t.Db.SaveChanges();
        t.Db.Add(New(OtherCustomerId, "someone else's"));
        t.Db.SaveChanges();
        var shown = New(CustomerId, "second");
        t.Db.Add(shown);
        t.Db.SaveChanges();
        t.Db.Add(New(CustomerId, "arrived later"));
        t.Db.SaveChanges();
        t.Db.ChangeTracker.Clear();
        var mine = new NotificationService(t.Uow, Customer);

        var before = await mine.ListMineAsync(default);
        await mine.MarkReadAsync(new ReadNotificationsRequest(shown.NotificationId), default);
        var after = await mine.ListMineAsync(default);

        before.Value!.Items.Select(n => n.Title).Should().Equal("arrived later", "second", "first");
        before.Value.UnreadCount.Should().Be(3);
        after.Value!.UnreadCount.Should().Be(1);
        Saved(t).Single(n => n.UserId == OtherCustomerId).ReadAt.Should().BeNull("it is not theirs to read");
    }
}
