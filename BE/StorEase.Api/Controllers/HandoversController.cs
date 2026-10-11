using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.Handovers;
using StorEase.Application.Features.Handovers.Dtos;

namespace StorEase.Api.Controllers;

[Route("api/handovers")]
public class HandoversController(IHandoverService handovers) : ApiControllerBase
{
    [HttpPost]
    [Authorize(Roles = Roles.Staff)]
    [ProducesResponseType<HandoverDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<HandoverDto>> Create(CreateHandoverRequest request, CancellationToken ct)
    {
        var result = await handovers.CreateAsync(request, ct);
        // No GET endpoint for a handover yet, so 201 without a Location header.
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : Failure(result.Error);
    }
}
