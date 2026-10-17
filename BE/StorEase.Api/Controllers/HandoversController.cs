using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;
using StorEase.Application.Features.Handovers;
using StorEase.Application.Features.Handovers.Dtos;

namespace StorEase.Api.Controllers;

[Route("api/handovers")]
public class HandoversController(IHandoverService handovers, IHandoverEvidenceService evidence) : ApiControllerBase
{
    [HttpPost]
    [Authorize(Roles = Roles.Staff)]
    [ProducesResponseType<HandoverDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<HandoverDto>> Create(CreateHandoverRequest request, CancellationToken ct)
    {
        var result = await handovers.CreateAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value.HandoverId }, result.Value)
            : Failure(result.Error);
    }

    [HttpGet("{id:int}")]
    [Authorize]
    public async Task<ActionResult<HandoverDetailDto>> Get(int id, CancellationToken ct)
    {
        var result = await evidence.GetAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>One condition photo (JPEG, PNG or WebP, up to 5 MB) as multipart form field <c>file</c>.</summary>
    [HttpPost("{id:int}/photos")]
    [Authorize(Roles = Roles.Staff)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(HandoverEvidenceService.MaxBytes + 64 * 1024)]
    [ProducesResponseType<HandoverPhotoDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<HandoverPhotoDto>> AddPhoto(int id, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var result = await evidence.AddPhotoAsync(id, stream, file.FileName, file.ContentType, file.Length, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Photo), new { id, photoId = result.Value.HandoverPhotoId }, result.Value)
            : Failure(result.Error);
    }

    [HttpGet("{id:int}/photos/{photoId:int}")]
    [Authorize]
    public async Task<IActionResult> Photo(int id, int photoId, CancellationToken ct)
    {
        var result = await evidence.OpenPhotoAsync(id, photoId, ct);
        return result.IsSuccess ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName) : Failure(result.Error);
    }
}
