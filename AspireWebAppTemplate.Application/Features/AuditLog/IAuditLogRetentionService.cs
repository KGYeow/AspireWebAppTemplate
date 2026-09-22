namespace AspireWebAppTemplate.Application.Features.AuditLog;

/// <summary>
/// Defines the contract for the audit log retention service that manages audit-log data
/// retention by purging entries older than the configured retention period.
/// </summary>
/// <remarks>
/// This service is intentionally focused: it depends only on the database context and application
/// configuration, carrying no identity or display-name concerns. This keeps the purge/retention
/// responsibility decoupled from the audit-write path, so a short-lived background caller (e.g., a
/// scheduled batch job) can invoke it without pulling in ASP.NET Core Identity. Implementations
/// should be registered as scoped services to align with the per-request <c>DbContext</c> lifetime.
/// </remarks>
public interface IAuditLogRetentionService
{
    #region Retention

    /// <summary>
    /// Purges audit log entries older than the configured retention period. The retention window is
    /// read from the <c>AuditLog:RetentionDays</c> configuration value (validated to the range 1–3650,
    /// falling back to a default of 365 days when missing, non-numeric, or out of range). All entries
    /// whose timestamp is older than the computed cutoff are deleted, and the number of deleted entries
    /// is returned.
    /// </summary>
    /// <returns>
    /// A task that resolves to the number of audit log entries that were deleted from the database.
    /// </returns>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when the underlying database delete operation fails due to connectivity or concurrency
    /// issues. Purge failures are propagated to the caller (rather than swallowed) so that the invoking
    /// process (e.g., a background job) can implement retry logic.
    /// </exception>
    Task<int> PurgeOldEntriesAsync();

    #endregion
}
