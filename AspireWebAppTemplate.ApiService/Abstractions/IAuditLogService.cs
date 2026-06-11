using AspireWebAppTemplate.Core.Domain.Enums;

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
    /// Records a single audit log entry into the database.
    /// Failures are logged at Error level but never propagated to the caller,
    /// ensuring audit failures do not disrupt the primary user operation.
    /// </summary>
    /// <param name="userId">
    /// The identifier of the acting user, or <c>null</c> for system-generated events
    /// where no authenticated user context exists.
    /// </param>
    /// <param name="actionType">The category of action being recorded (e.g., UserCreated, LoginSuccess).</param>
    /// <param name="entityType">The type of entity affected by the action (e.g., User, Role, Settings).</param>
    /// <param name="entityId">
    /// The unique identifier of the affected entity (e.g., user ID, role ID).
    /// Maximum 450 characters.
    /// </param>
    /// <param name="entityName">
    /// A human-readable name for the affected entity (e.g., user display name, role name).
    /// Maximum 256 characters.
    /// </param>
    /// <param name="description">
    /// A brief human-readable summary of what occurred.
    /// Maximum 1024 characters.
    /// </param>
    /// <param name="oldValues">
    /// JSON-serialized representation of the entity's previous state before the action,
    /// or <c>null</c> if not applicable (e.g., for creation or login events).
    /// </param>
    /// <param name="newValues">
    /// JSON-serialized representation of the entity's new state after the action,
    /// or <c>null</c> if not applicable (e.g., for deletion or login events).
    /// </param>
    /// <param name="ipAddress">
    /// The source IP address of the HTTP request that triggered the action,
    /// or <c>null</c> if not available. Maximum 45 characters (supports IPv6).
    /// </param>
    /// <returns>A task representing the asynchronous logging operation.</returns>
    /// <exception cref="System.Exception">
    /// This method does not throw exceptions. Any database errors encountered during
    /// persistence are caught, logged at Error level via <c>ILogger</c>, and swallowed.
    /// </exception>
    Task LogAsync(
        string? userId,
        AuditActionType actionType,
        AuditEntityType entityType,
        string entityId,
        string entityName,
        string description,
        string? oldValues = null,
        string? newValues = null,
        string? ipAddress = null);

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
