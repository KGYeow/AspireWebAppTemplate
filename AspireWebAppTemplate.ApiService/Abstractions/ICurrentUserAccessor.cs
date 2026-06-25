namespace AspireWebAppTemplate.ApiService.Abstractions;

/// <summary>
/// Provides the authenticated user's identity information to service-layer components.
/// Backed by IHttpContextAccessor, returns null for all properties when no HTTP context
/// or authenticated user is available.
/// </summary>
/// <remarks>
/// Implementations should be registered as scoped services to align with the per-request
/// lifecycle. Services inject this interface to obtain the current user's identity for
/// audit logging and ownership checks, eliminating the need to pass userId and ipAddress
/// through every method signature.
/// </remarks>
public interface ICurrentUserAccessor
{
    #region Operations

    /// <summary>The authenticated user's ID from ClaimTypes.NameIdentifier.</summary>
    string? UserId { get; }

    /// <summary>The authenticated user's display name from Identity.Name.</summary>
    string? UserName { get; }

    /// <summary>The client's IP address from HttpContext.Connection.RemoteIpAddress.</summary>
    string? IpAddress { get; }

    #endregion
}
