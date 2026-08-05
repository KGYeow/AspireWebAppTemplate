using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Application.Contracts.AuditLog;

namespace AspireWebAppTemplate.Application.Abstractions;

/// <summary>
/// Defines the contract for the audit log service that records significant user and system
/// actions into a persistent audit trail, manages data retention through periodic purging,
/// and provides query/filter/export capabilities for audit log entries.
/// </summary>
/// <remarks>
/// Implementations should be registered as scoped services to align with the per-request
/// <c>DbContext</c> lifetime in Blazor Server circuits. The <see cref="LogAsync"/> method
/// is designed to be fire-and-forget safe—failures are logged but never propagated to the caller.
/// </remarks>
public interface IAuditLogService
{
    #region Write Operations

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

    #endregion

    #region Query Operations

    /// <summary>
    /// Returns a paged list of audit log entries with optional filtering.
    /// Entries are ordered by timestamp descending (newest first).
    /// </summary>
    /// <param name="queryParams">
    /// An <see cref="AuditLogQueryParams"/> containing pagination (page, pageSize)
    /// and optional filter criteria (search term, action type, entity type, date range).
    /// </param>
    /// <returns>
    /// A task that resolves to a <see cref="PagedResult{T}"/> of <see cref="AuditLogEntryDto"/>
    /// containing the matching entries and total count metadata.
    /// </returns>
    Task<PagedResult<AuditLogEntryDto>> SearchAsync(AuditLogQueryParams queryParams);

    /// <summary>
    /// Returns a single audit log entry by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the audit log entry to retrieve.</param>
    /// <returns>
    /// A task that resolves to the <see cref="AuditLogEntryDto"/> matching the specified ID.
    /// </returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no audit log entry exists with the given <paramref name="id"/>.
    /// </exception>
    Task<AuditLogEntryDto> GetByIdAsync(Guid id);

    /// <summary>
    /// Returns filtered audit log entries for export, capped at
    /// <see cref="Domain.Constants.ExportDefaults.MaxExportRows"/>.
    /// Entries are ordered by timestamp descending (newest first).
    /// </summary>
    /// <param name="queryParams">
    /// An <see cref="AuditLogQueryParams"/> containing optional filter criteria
    /// (search term, action type, entity type, date range). Pagination properties are ignored.
    /// </param>
    /// <returns>
    /// A task that resolves to a list of <see cref="AuditLogEntryDto"/> records matching
    /// the filter criteria, limited to the maximum export row count.
    /// </returns>
    Task<List<AuditLogEntryDto>> GetForExportAsync(AuditLogQueryParams queryParams);

    #endregion
}
