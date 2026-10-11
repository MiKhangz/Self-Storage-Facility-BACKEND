using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.Reservations;
using StorEase.Application.Features.Reservations.Dtos;
using StorEase.Domain.Enums;

namespace StorEase.Api.Controllers;

[Route("api/reservations")]
public class ReservationsController(IReservationService reservations) : ApiControllerBase
{
    [HttpPost]
    [Authorize(Roles = Roles.StorageCustomer)]
    public async Task<ActionResult<ReservationDto>> Create(CreateReservationRequest request, CancellationToken ct)
    {
        var result = await reservations.CreateAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value.ReservationId }, result.Value)
            : Failure(result.Error);
    }

    /// <summary>Staff see every reservation; a customer sees only their own.</summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<PagedResult<ReservationDto>>> List(
        ReservationStatus? status, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await reservations.ListAsync(status, page, pageSize, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpGet("{id:int}")]
    [Authorize]
    public async Task<ActionResult<ReservationDto>> Get(int id, CancellationToken ct)
    {
        var result = await reservations.GetAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost("{id:int}/assign-unit")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<ReservationDto>> AssignUnit(int id, AssignUnitRequest request, CancellationToken ct)
    {
        var result = await reservations.AssignUnitAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
}
