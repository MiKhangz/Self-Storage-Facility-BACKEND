using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Features.SupportTickets;
using StorEase.Application.Features.SupportTickets.Dtos;

namespace StorEase.Api.Controllers;

[Route("api/ticket-categories")]
public class TicketCategoriesController(ISupportTicketService tickets) : ApiControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<TicketCategoryDto>>> List(CancellationToken ct)
    {
        var result = await tickets.ListCategoriesAsync(ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
}
