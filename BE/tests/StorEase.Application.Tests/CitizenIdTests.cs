using FluentAssertions;
using StorEase.Application.Common;
using StorEase.Application.Features.Invoices;
using StorEase.Application.Features.Reservations;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using StorEase.Infrastructure.Services;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

public class CitizenIdTests
{
    private const string Full = "079203001234";
    private const string Masked = "********1234";

    private static WalkInService As(TestDb t, int userId, string role)
    {
        var user = new FakeCurrentUser(userId, role);
        return new WalkInService(t.Uow, user, new Pbkdf2PasswordHasher(), new InvoiceService(t.Uow, user, new PaymentGatewayOptions()),
            new ReservationService(t.Uow, user, new FakeEmailService()));
    }

    private static TestDb Seed(bool bookedHere)
    {
        var t = Create();
        t.Db.Users.Find(CustomerId)!.CitizenId = Full;
        if (bookedHere)
            t.Db.Add(new Reservation
            {
                CustomerId = CustomerId, FacilityId = FacilityId, UnitTypeId = SmallTypeId, ReservationCode = "RS-ID",
                MoveInDate = VietnamTime.Today, RentalMonths = 1, Status = ReservationStatus.Confirmed, CreatedAt = DateTime.UtcNow,
            });
        t.Db.SaveChanges();
        return t;
    }

    [Fact]
    public async Task The_desk_search_never_lists_whole_citizen_ids()
    {
        using var t = Seed(bookedHere: true);

        var found = await As(t, StaffId, Roles.FacilityStaff).SearchCustomersAsync(null, 1, 20, default);

        found.Value!.Items.Single(c => c.UserId == CustomerId).CitizenId.Should().Be(Masked);
    }

    [Theory]
    [InlineData(StaffId, Roles.FacilityStaff, true, Full)]
    [InlineData(StaffId, Roles.FacilityStaff, false, Masked)]
    [InlineData(77, Roles.FacilityManager, true, Masked)] // manages another facility
    [InlineData(88, Roles.BusinessOperationsManager, false, Full)]
    [InlineData(99, Roles.SystemAdministrator, false, Full)]
    public async Task The_whole_id_is_for_staff_where_the_customer_booked(int userId, string role, bool bookedHere, string expected)
    {
        using var t = Seed(bookedHere);

        (await As(t, userId, role).GetCustomerAsync(CustomerId, default)).Value!.CitizenId.Should().Be(expected);
    }
}
