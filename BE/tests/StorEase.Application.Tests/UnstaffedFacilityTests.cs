using FluentAssertions;
using StorEase.Application.Common;
using StorEase.Application.Features.Facilities;
using StorEase.Application.Features.Reservations;
using StorEase.Application.Features.Reservations.Dtos;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using static StorEase.Application.Tests.CoreFlowSeed;

namespace StorEase.Application.Tests;

/// <summary>A facility nobody works at is hidden from customers and takes no bookings, so no request reaches nobody.</summary>
public class UnstaffedFacilityTests
{
    [Fact]
    public async Task Customers_neither_see_nor_book_a_facility_without_staff_while_staff_see_it_flagged()
    {
        using var t = Create();
        t.Db.Add(new Facility { FacilityId = 2, Code = "NEW", Name = "Nobody here", Address = "2 Test", District = "Q1", Status = FacilityStatus.Active });
        t.Db.SaveChanges();

        var customerList = await new FacilityService(t.Uow, Customer).ListAsync(null, null, false, 1, 20, default);
        var adminList = await new FacilityService(t.Uow, new FakeCurrentUser(99, Roles.SystemAdministrator)).ListAsync(null, null, false, 1, 20, default);
        var detail = await new FacilityService(t.Uow, Customer).GetAsync(2, default);
        var booking = await new ReservationService(t.Uow, Customer, new FakeEmailService())
            .CreateAsync(new CreateReservationRequest(2, SmallTypeId, MoveIn, 1), default);

        customerList.Value!.Items.Select(f => f.FacilityId).Should().Equal(FacilityId);
        adminList.Value!.Items.Single(f => f.FacilityId == 2).Staffed.Should().BeFalse();
        adminList.Value.Items.Single(f => f.FacilityId == FacilityId).Staffed.Should().BeTrue();
        detail.Error!.Type.Should().Be(ErrorType.NotFound);
        booking.Error!.Type.Should().Be(ErrorType.Conflict);
    }
}
