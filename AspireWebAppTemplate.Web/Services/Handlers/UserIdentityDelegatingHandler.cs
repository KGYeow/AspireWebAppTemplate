using System.Security.Claims;

namespace AspireWebAppTemplate.Web.Services;

/// <summary>
/// Forwards the authenticated user's identity and client IP address from the Web project's
/// cookie session to the API service via custom headers on outbound HTTP requests.
/// This enables service-to-service identity propagation in the Aspire architecture.
/// </summary>
/// <remarks>
/// <para>
/// <b>Identity resolution strategy (in priority order):</b>
/// <list type="number">
///   <item><see cref="CircuitUserContext"/> — the circuit-scoped cached principal and IP, available
///         after initialization and throughout the circuit's WebSocket lifetime.</item>
///   <item><see cref="IHttpContextAccessor.HttpContext"/> — the HTTP request context, available
///         only during the initial SSR render before the SignalR circuit establishes.</item>
/// </list>
/// </para>
/// <para>
/// This dual-source approach solves the Blazor Server issue where <c>HttpContext</c> becomes null
/// after the WebSocket connection replaces the initial HTTP request. During SSR pre-render,
/// <c>HttpContext</c> is used; once the circuit is live, <c>CircuitUserContext</c> provides the identity.
/// </para>
/// <para>
/// <b>Client IP propagation:</b> The handler forwards the end-user's real IP address as an
/// <c>X-Client-Ip</c> header so that the API service can use it for audit logging. Without this,
/// the API service would only see the Web server's internal IP. The IP is resolved from
/// <see cref="CircuitUserContext.ClientIpAddress"/> (cached at circuit start) or from
/// <see cref="Microsoft.AspNetCore.Http.ConnectionInfo.RemoteIpAddress"/> during SSR.
/// </para>
/// </remarks>
public class UserIdentityDelegatingHandler : DelegatingHandler
{
    #region Constructor

    /// <summary>
    /// Provides access to the HTTP context for identity resolution during the initial SSR render.
    /// </summary>
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Circuit-scoped cached user principal and IP, available throughout the circuit's WebSocket lifetime.
    /// </summary>
    private readonly CircuitUserContext _circuitUserContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserIdentityDelegatingHandler"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">Provides access to the HTTP context during SSR.</param>
    /// <param name="circuitUserContext">Circuit-scoped cached user principal and IP.</param>
    public UserIdentityDelegatingHandler(IHttpContextAccessor httpContextAccessor, CircuitUserContext circuitUserContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _circuitUserContext = circuitUserContext;
    }

    #endregion

    /// <summary>
    /// Attaches the authenticated user's identity claims and client IP address as custom headers
    /// on the outbound HTTP request before forwarding it to the API service.
    /// </summary>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Resolve user identity: prefer CircuitUserContext (available throughout circuit lifetime),
        // fall back to HttpContext (available only during initial SSR render).
        var user = _circuitUserContext.User
            ?? _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated == true)
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = user.FindFirstValue(ClaimTypes.Name);
            var email = user.FindFirstValue(ClaimTypes.Email);
            var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value);

            if (!string.IsNullOrEmpty(userId))
                request.Headers.TryAddWithoutValidation("X-User-Id", userId);

            if (!string.IsNullOrEmpty(userName))
                request.Headers.TryAddWithoutValidation("X-User-Name", userName);

            if (!string.IsNullOrEmpty(email))
                request.Headers.TryAddWithoutValidation("X-User-Email", email);

            var roleList = string.Join(",", roles);
            if (!string.IsNullOrEmpty(roleList))
                request.Headers.TryAddWithoutValidation("X-User-Roles", roleList);
        }

        // Forward the end-user's client IP address so the API service can use it for audit logging.
        // Prefer the cached IP from CircuitUserContext (available after WebSocket takes over),
        // fall back to HttpContext.Connection.RemoteIpAddress during initial SSR render.
        var clientIp = _circuitUserContext.ClientIpAddress
            ?? _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();

        if (!string.IsNullOrEmpty(clientIp))
            request.Headers.TryAddWithoutValidation("X-Client-Ip", clientIp);

        return base.SendAsync(request, cancellationToken);
    }
}
