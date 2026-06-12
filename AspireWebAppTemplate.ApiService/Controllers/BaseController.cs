using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace AspireWebAppTemplate.ApiService.Controllers;

/// <summary>
/// Base controller providing shared helper properties for authenticated API endpoints.
/// All application controllers should inherit from this instead of <see cref="ControllerBase"/>.
/// </summary>
[ApiController]
public abstract class BaseController : ControllerBase
{
    /// <summary>
    /// The authenticated user's ID from the request claims. Null if unauthenticated.
    /// </summary>
    protected string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>
    /// The authenticated user's display name from the request claims.
    /// </summary>
    protected string? CurrentUserName => User.Identity?.Name;

    /// <summary>
    /// The client's IP address from the current HTTP connection.
    /// </summary>
    protected string? ClientIpAddress => HttpContext.Connection.RemoteIpAddress?.ToString();
}
