using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.SupportTickets;
using StorEase.Application.Features.SupportTickets.Dtos;
using StorEase.Domain.Enums;

namespace StorEase.Api.Controllers;

/// <summary>Queue, assignment and status of support tickets. Create / read / reply live in SupportTicketsController.</summary>
[Route("api/support-tickets")]
public class TicketQueueController(ITicketQueueService queue) : ApiControllerBase
{
    /// <summary>Staff see tickets of their facilities, highest priority first; a customer sees only their own.</summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<PagedResult<TicketQueueItemDto>>> List(
        SupportTicketStatus? status, Priority? priority, int? facilityId, bool assignedToMe = false,
        int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await queue.ListAsync(status, priority, facilityId, assignedToMe, page, pageSize, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost("{id:int}/assign")]
    [Authorize(Roles = Roles.FacilityManager)]
    public async Task<ActionResult<TicketQueueItemDto>> Assign(int id, AssignTicketRequest request, CancellationToken ct)
    {
        var result = await queue.AssignAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost("{id:int}/status")]
    [Authorize(Roles = Roles.StorageCustomer + "," + Roles.Staff)]
    public async Task<ActionResult<TicketQueueItemDto>> ChangeStatus(
        int id, ChangeTicketStatusRequest request, CancellationToken ct)
    {
        var result = await queue.ChangeStatusAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
}
