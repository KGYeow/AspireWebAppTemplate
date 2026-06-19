using System.Security.Claims;

namespace AspireWebAppTemplate.Web.Services;

/// <summary>
/// Forwards the authenticated user's identity from the Web project's cookie session
/// to the API service via custom headers on outbound HTTP requests.
/// This enables service-to-service identity propagation in the Aspire architecture.
/// </summary>
/// <remarks>
/// <para>
/// <b>Identity resolution strategy (in priority order):</b>
/// <list type="number">
///   <item><see cref="CircuitUserContext"/> — the circuit-scoped cached principal, available
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
/// </remarks>
public class UserIdentityDelegatingHandler(
    IHttpContextAccessor httpContextAccessor,
    CircuitUserContext circuitUserContext) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Resolve user identity: prefer CircuitUserContext (available throughout circuit lifetime),
        // fall back to HttpContext (available only during initial SSR render).
        var user = circuitUserContext.User
            ?? httpContextAccessor.HttpContext?.User;

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

        return base.SendAsync(request, cancellationToken);
    }
}
