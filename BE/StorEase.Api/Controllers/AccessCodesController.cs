using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.AccessCodes;
using StorEase.Application.Features.AccessCodes.Dtos;

namespace StorEase.Api.Controllers;

[Route("api/access-codes")]
public class AccessCodesController(IAccessCodeService accessCodes) : ApiControllerBase
{
    [HttpPost("{id:int}/revoke")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<AccessCodeSummaryDto>> Revoke(int id, CancellationToken ct)
    {
        var result = await accessCodes.RevokeAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
}
