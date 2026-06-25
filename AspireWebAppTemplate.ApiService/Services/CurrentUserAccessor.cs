using System.Security.Claims;
using AspireWebAppTemplate.ApiService.Abstractions;
using Microsoft.AspNetCore.Http;

namespace AspireWebAppTemplate.ApiService.Services;

/// <summary>
/// Implements <see cref="ICurrentUserAccessor"/> by reading identity claims and connection
/// information from the current <see cref="HttpContext"/> via <see cref="IHttpContextAccessor"/>.
/// </summary>
/// <remarks>
/// <para>
/// All properties return <c>null</c> when no HTTP context is available (e.g., background services)
/// or when no authenticated user is present on the request — no exceptions are thrown.
/// </para>
/// <para>
/// <strong>IP resolution strategy:</strong> The <c>X-Client-Ip</c> header takes priority because
/// the Web project's <c>UserIdentityDelegatingHandler</c> forwards the end-user's real IP address
/// in that header. The fallback to <c>Connection.RemoteIpAddress</c> supports direct API access
/// scenarios (e.g., integration tests, external clients).
/// </para>
/// <para>
/// Registered as a scoped service to align with the per-request lifecycle.
/// </para>
/// </remarks>
public class CurrentUserAccessor : ICurrentUserAccessor
{
    /// <summary>
    /// Provides access to the current HTTP context for reading user claims and connection info.
    /// </summary>
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrentUserAccessor"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">
    /// The HTTP context accessor used to retrieve the current request's user identity and connection details.
    /// </param>
    public CurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public string? UserId =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <inheritdoc />
    public string? UserName =>
        _httpContextAccessor.HttpContext?.User?.Identity?.Name;

    /// <inheritdoc />
    /// <remarks>
    /// Reads the client's real IP address from the <c>X-Client-Ip</c> header forwarded by the
    /// Web project's <c>UserIdentityDelegatingHandler</c>. Falls back to
    /// <c>Connection.RemoteIpAddress</c> only if the header is absent (e.g., direct API access
    /// during testing).
    /// </remarks>
    public string? IpAddress =>
        _httpContextAccessor.HttpContext?.Request?.Headers["X-Client-Ip"].FirstOrDefault()
        ?? _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();
}
