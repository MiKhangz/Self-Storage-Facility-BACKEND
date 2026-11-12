using FluentAssertions;
using StorEase.Api.Middleware;

namespace StorEase.Api.Tests;

public class ActivityLogFilterTests
{
    private sealed record Booking(int FacilityId, int ReservationId, string Code);
    private sealed record Place(int FacilityId, string Name);
    private sealed record Visit(int ContractId, int CheckInId);
    private sealed record Message(string Text);

    [Fact]
    public void A_created_record_is_found_by_the_controllers_name_else_by_the_first_id()
    {
        ActivityLogFilter.IdFromResponse("Reservations", new Booking(3, 41, "RS-1")).Should().Be("41");
        ActivityLogFilter.IdFromResponse("Facilities", new Place(7, "Q1")).Should().Be("7");
        ActivityLogFilter.IdFromResponse("CheckIns", new Visit(5, 12)).Should().Be("12");
        ActivityLogFilter.IdFromResponse("Shifts", new Visit(5, 12)).Should().Be("5");
        ActivityLogFilter.IdFromResponse("Payments", new Message("ok")).Should().BeNull();
        ActivityLogFilter.IdFromResponse("Payments", null).Should().BeNull();
    }
}
