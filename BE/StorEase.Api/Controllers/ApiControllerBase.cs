using Microsoft.AspNetCore.Mvc;
using StorEase.Application.Common;

namespace StorEase.Api.Controllers;

[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Maps a service <see cref="Error"/> to an RFC 7807 ProblemDetails response.</summary>
    protected ObjectResult Failure(Error error) => Problem(
        title: ErrorText.Localize(error.Message),
        statusCode: error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.NotImplemented => StatusCodes.Status501NotImplemented,
            _ => StatusCodes.Status500InternalServerError,
        });
}
