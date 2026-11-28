using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.Reservations;
using StorEase.Application.Features.Reservations.Dtos;
using StorEase.Application.Features.StorageUnits.Dtos;
using StorEase.Domain.Enums;

namespace StorEase.Api.Controllers;

[Route("api/reservations")]
public class ReservationsController(IReservationService reservations, IWalkInService walkIns) : ApiControllerBase
{
    /// <summary>
    /// A booking made at the desk for an existing or a new customer: Confirmed with the chosen unit at once, the
    /// deposit recorded as paid in cash. Staff at their own facilities only.
    /// </summary>
    [HttpPost("walk-in")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<ReservationDto>> WalkIn(WalkInReservationRequest request, CancellationToken ct)
    {
        var result = await walkIns.BookAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value.ReservationId }, result.Value)
            : Failure(result.Error);
    }

    [HttpPost]
    [Authorize(Roles = Roles.StorageCustomer)]
    public async Task<ActionResult<ReservationDto>> Create(CreateReservationRequest request, CancellationToken ct)
    {
        var result = await reservations.CreateAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value.ReservationId }, result.Value)
            : Failure(result.Error);
    }

    /// <summary>
    /// Staff see every reservation; a customer sees only their own. <c>search</c> matches the reservation code,
    /// customer name, phone or email; <c>moveInFrom</c>/<c>moveInTo</c> are inclusive.
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<PagedResult<ReservationDto>>> List(
        ReservationStatus? status, string? search, int? facilityId, DateOnly? moveInFrom, DateOnly? moveInTo,
        int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await reservations.ListAsync(status, search, facilityId, moveInFrom, moveInTo, page, pageSize, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpGet("{id:int}")]
    [Authorize]
    public async Task<ActionResult<ReservationDto>> Get(int id, CancellationToken ct)
    {
        var result = await reservations.GetAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpGet("{id:int}/available-units")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<IReadOnlyList<UnitSlotDto>>> AvailableUnits(int id, CancellationToken ct)
    {
        var result = await reservations.AvailableUnitsAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>The customer (own reservation) or staff cancel; a held unit is released and a Draft contract ends.</summary>
    [HttpPost("{id:int}/cancel")]
    [Authorize(Roles = Roles.StorageCustomer + "," + Roles.Staff)]
    public async Task<ActionResult<ReservationCancellationDto>> Cancel(int id, CancelReservationRequest request, CancellationToken ct)
    {
        var result = await reservations.CancelAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>After the move-in date passes without a handover: the reservation expires and the deposit is forfeited.</summary>
    [HttpPost("{id:int}/no-show")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<ReservationCancellationDto>> NoShow(int id, CancellationToken ct)
    {
        var result = await reservations.MarkNoShowAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>Request a refund on a closed same-day booking (after 6h cancel or no-show: monthly rent minus 1-day deposit).</summary>
    [HttpPost("{id:int}/request-refund")]
    [Authorize(Roles = Roles.StorageCustomer)]
    public async Task<ActionResult<ReservationCancellationDto>> RequestRefund(int id, CancellationToken ct)
    {
        var result = await reservations.RequestRefundAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>The counter checklist staff complete before handover.</summary>
    /// <summary>The customer accepts the unit offered instead of the expected one.</summary>
    [HttpPost("{id:int}/accept-unit")]
    [Authorize(Roles = Roles.StorageCustomer)]
    public async Task<ActionResult<ReservationDto>> AcceptUnit(int id, CancellationToken ct)
    {
        var result = await reservations.AcceptProposedUnitAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>The customer turns the offered unit down: the booking is cancelled and the whole deposit refunded.</summary>
    [HttpPost("{id:int}/decline-unit")]
    [Authorize(Roles = Roles.StorageCustomer)]
    public async Task<ActionResult<ReservationCancellationDto>> DeclineUnit(int id, CancellationToken ct)
    {
        var result = await reservations.DeclineProposedUnitAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>The handover appointment (Vietnam time); missing it forfeits the deposit.</summary>
    [HttpPost("{id:int}/appointment")]
    [Authorize(Roles = Roles.StorageCustomer + "," + Roles.Staff)]
    public async Task<ActionResult<ReservationDto>> Appointment(int id, SetAppointmentRequest request, CancellationToken ct)
    {
        var result = await reservations.SetAppointmentAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpGet("{id:int}/handover")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<HandoverChecklistDto>> Handover(int id, CancellationToken ct)
    {
        var result = await reservations.GetHandoverChecklistAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost("{id:int}/assign-unit")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<ReservationDto>> AssignUnit(int id, AssignUnitRequest request, CancellationToken ct)
    {
        var result = await reservations.AssignUnitAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>At the handover: the customer saw the unit and prefers another free one of the same size.</summary>
    [HttpPost("{id:int}/change-unit")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<ReservationDto>> ChangeUnit(int id, AssignUnitRequest request, CancellationToken ct)
    {
        var result = await reservations.ChangeUnitAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>Update move-in date or rental period on an open reservation; re-checks capacity and updates any linked ticket.</summary>
    [HttpPost("{id:int}/dates")]
    [Authorize(Roles = Roles.StorageCustomer + "," + Roles.Staff)]
    public async Task<ActionResult<ReservationDto>> UpdateDates(int id, UpdateReservationDatesRequest request, CancellationToken ct)
    {
        var result = await reservations.UpdateDatesAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
}
