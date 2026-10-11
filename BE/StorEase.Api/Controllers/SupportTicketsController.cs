using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.SupportTickets;
using StorEase.Application.Features.SupportTickets.Dtos;

namespace StorEase.Api.Controllers;

[Route("api/support-tickets")]
public class SupportTicketsController(ISupportTicketService tickets) : ApiControllerBase
{
    [HttpPost]
    [Authorize(Roles = Roles.StorageCustomer)]
    public async Task<ActionResult<TicketDto>> Create(CreateTicketRequest request, CancellationToken ct)
    {
        var result = await tickets.CreateAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value.TicketId }, result.Value)
            : Failure(result.Error);
    }

    [HttpGet("{id:int}")]
    [Authorize]
    public async Task<ActionResult<TicketDto>> Get(int id, CancellationToken ct)
    {
        var result = await tickets.GetAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpGet("{id:int}/messages")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<TicketMessageDto>>> ListMessages(int id, CancellationToken ct)
    {
        var result = await tickets.ListMessagesAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost("{id:int}/messages")]
    [Authorize]
    public async Task<ActionResult<TicketMessageDto>> PostMessage(int id, PostTicketMessageRequest request, CancellationToken ct)
    {
        var result = await tickets.PostMessageAsync(id, request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(ListMessages), new { id }, result.Value)
            : Failure(result.Error);
    }
}
