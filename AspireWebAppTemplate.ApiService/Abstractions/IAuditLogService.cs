using AspireWebAppTemplate.Core.Contracts.AuditLog;

namespace AspireWebAppTemplate.Abstractions;

/// <summary>
/// Defines the contract for the audit log service that records significant user and system
/// actions into a persistent audit trail and manages data retention through periodic purging.
/// </summary>
/// <remarks>
/// Implementations should be registered as scoped services to align with the per-request
/// <c>DbContext</c> lifetime in Blazor Server circuits. The <see cref="LogAsync"/> method
/// is designed to be fire-and-forget safe—failures are logged but never propagated to the caller.
/// </remarks>
public interface IAuditLogService
{
    /// <summary>
    /// Records a single audit log entry into the database using the provided request object.
    /// Failures are logged at Error level but never propagated to the caller,
    /// ensuring audit failures do not disrupt the primary user operation.
    /// </summary>
    /// <param name="request">
    /// An <see cref="AuditLogRequest"/> instance containing all parameters for the audit entry,
    /// including the acting user, action type, affected entity details, optional old/new values,
    /// and the source IP address.
    /// </param>
    /// <returns>A task representing the asynchronous logging operation.</returns>
    /// <exception cref="System.Exception">
    /// This method does not throw exceptions. Any database errors encountered during
    /// persistence are caught, logged at Error level via <c>ILogger</c>, and swallowed.
    /// </exception>
    Task LogAsync(AuditLogRequest request);

    /// <summary>
    /// Purges audit log entries older than the configured retention period
    /// (<c>AuditLog:RetentionDays</c> in appsettings.json, defaulting to 365 days).
    /// </summary>
    /// <returns>
    /// The number of audit log entries that were deleted from the database.
    /// </returns>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when the database operation fails due to connectivity or concurrency issues.
    /// Unlike <see cref="LogAsync"/>, purge failures are propagated to the caller so that
    /// the invoking process (e.g., a background job) can handle retry logic.
    /// </exception>
    Task<int> PurgeOldEntriesAsync();
}
