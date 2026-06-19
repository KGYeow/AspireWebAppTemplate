using System.Security.Claims;

namespace AspireWebAppTemplate.Web.Services;

/// <summary>
/// Circuit-scoped service that captures and caches the authenticated user's claims
/// for the lifetime of a Blazor Server circuit. This solves the fundamental issue where
/// <see cref="IHttpContextAccessor.HttpContext"/> becomes null after the SignalR WebSocket
/// connection replaces the initial HTTP request.
/// </summary>
/// <remarks>
/// <para>
/// <b>Problem:</b> In Blazor Server, <c>HttpContext</c> is only available during the initial
/// HTTP request (SSR pre-render). Once the circuit establishes over WebSocket, there is no
/// HTTP context. Services that depend on <c>IHttpContextAccessor</c> (like delegating handlers
/// for identity propagation) intermittently fail with null reference or missing identity.
/// </para>
/// <para>
/// <b>Solution:</b> This scoped service captures the user's <see cref="ClaimsPrincipal"/> during
/// the initial render (when <c>HttpContext</c> is still available) and retains it for the circuit's
/// lifetime. The <see cref="UserIdentityDelegatingHandler"/> reads from this cache first, falling
/// back to <c>HttpContext</c> only during the initial SSR pass.
/// </para>
/// </remarks>
public sealed class CircuitUserContext
{
    private ClaimsPrincipal? _user;

    /// <summary>
    /// Gets the cached <see cref="ClaimsPrincipal"/> for the current circuit.
    /// Returns null if <see cref="Initialize"/> has not been called yet.
    /// </summary>
    public ClaimsPrincipal? User => _user;

    /// <summary>
    /// Gets whether the user has been initialized (captured) for this circuit.
    /// </summary>
    public bool IsInitialized => _user is not null;

    /// <summary>
    /// Captures the user's claims principal for the duration of this circuit.
    /// Should be called exactly once, early in the circuit lifecycle (e.g., MainLayout.OnInitializedAsync).
    /// Subsequent calls are no-ops to prevent accidental overwrites.
    /// </summary>
    /// <param name="user">The authenticated user's claims principal from the auth state provider.</param>
    public void Initialize(ClaimsPrincipal user)
    {
        // Only capture once per circuit — prevent race conditions or accidental overwrites
        _user ??= user;
    }
}
