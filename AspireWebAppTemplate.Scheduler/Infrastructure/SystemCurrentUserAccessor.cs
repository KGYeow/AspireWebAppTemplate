using AspireWebAppTemplate.Application.Abstractions;

namespace AspireWebAppTemplate.Scheduler.Infrastructure;

/// <summary>
/// Provides a fixed "system" identity for the Scheduler process. The web-based
/// <c>CurrentUserAccessor</c> reads the user from <c>IHttpContextAccessor</c>, but batch jobs
/// run without any HTTP request, so any service method that audit-logs needs a system principal
/// instead of a real user.
/// </summary>
/// <remarks>
/// Registered in place of the HTTP-backed accessor for the Scheduler host only. Audit entries
/// produced by jobs are therefore attributed to "System" with no IP address.
/// </remarks>
public sealed class SystemCurrentUserAccessor : ICurrentUserAccessor
{
    /// <summary>The stable identifier attributed to actions performed by the Scheduler.</summary>
    public string? UserId => "system:scheduler";

    /// <summary>The display name attributed to Scheduler-initiated actions.</summary>
    public string? UserName => "System (Scheduler)";

    /// <summary>Always null — batch jobs have no client IP address.</summary>
    public string? IpAddress => null;
}