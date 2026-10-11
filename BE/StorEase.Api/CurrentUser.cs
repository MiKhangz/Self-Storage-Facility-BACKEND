using System.Security.Claims;
using StorEase.Application.Interfaces;

namespace StorEase.Api;

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal Principal =>
        accessor.HttpContext?.User ?? throw new InvalidOperationException("No HTTP request in scope.");

    public int UserId => int.Parse(
        Principal.FindFirstValue("sub") ?? throw new InvalidOperationException("Token has no 'sub' claim."));

    public string Role => Principal.FindFirstValue("role") ?? string.Empty;
}
