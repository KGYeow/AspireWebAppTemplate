using BlazorWebAppTemplate.Core.Domain.Enums;

namespace BlazorWebAppTemplate.Data.Entities;

/// <summary>
/// Represents a single audit log record capturing a significant action performed within the application.
/// Each entry records who performed an action, what was done, which entity was affected, and when it occurred.
/// </summary>
/// <remarks>
/// Configured in <c>ApplicationDbContext.OnModelCreating</c> with indexes on Timestamp, UserId, and ActionType
/// for efficient querying. The UserId foreign key uses restrict delete behavior to preserve audit history
/// even when a user is removed from the system.
/// </remarks>
public class AuditLogEntry
{
    /// <summary>
    /// Gets or sets the unique identifier for this audit log entry.
    /// </summary>
    /// <remarks>
    /// Primary key. Generated as a new <see cref="Guid"/> upon creation.
    /// </remarks>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who performed the audited action.
    /// </summary>
    /// <remarks>
    /// Foreign key referencing <c>ApplicationUser.Id</c>. Maximum length: 450 characters.
    /// May reference a user that no longer exists if the FK uses restrict delete behavior.
    /// For system-level events where no user is associated, this will be an empty string.
    /// </remarks>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the user at the time the action was performed.
    /// </summary>
    /// <remarks>
    /// Denormalized from <see cref="ApplicationUser.DisplayName"/> for display performance
    /// and to preserve the name as it was at the time of the event (even if the user is later
    /// renamed or deleted). Maximum length: 256 characters.
    /// </remarks>
    public string UserDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the category of action that was performed.
    /// </summary>
    /// <remarks>
    /// Stored as a PascalCase string in the database via <c>HasConversion&lt;string&gt;()</c>
    /// for readability in raw SQL queries and to avoid breaking data if enum integer values shift.
    /// </remarks>
    public AuditActionType ActionType { get; set; }

    /// <summary>
    /// Gets or sets the type of entity that was affected by the audited action.
    /// </summary>
    /// <remarks>
    /// Stored as a PascalCase string in the database via <c>HasConversion&lt;string&gt;()</c>
    /// for readability in raw SQL queries and to avoid breaking data if enum integer values shift.
    /// </remarks>
    public AuditEntityType EntityType { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the entity affected by the action.
    /// </summary>
    /// <remarks>
    /// Maximum length: 450 characters. For user-related actions, this is typically the user's ID.
    /// For role actions, this may be the user's ID with the role name in <see cref="EntityName"/>.
    /// For failed login attempts, this is the attempted username or email.
    /// </remarks>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-readable name of the entity affected by the action.
    /// </summary>
    /// <remarks>
    /// Maximum length: 256 characters. Provides a friendly display value for the affected entity
    /// (e.g., user's display name, role name, or setting key).
    /// </remarks>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a human-readable summary describing what occurred.
    /// </summary>
    /// <remarks>
    /// Maximum length: 1024 characters. Provides context about the action in plain language
    /// (e.g., "User 'John Doe' was assigned the 'Admin' role.").
    /// </remarks>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the JSON-serialized previous state of the entity before the action, if applicable.
    /// </summary>
    /// <remarks>
    /// Nullable. Contains a JSON representation of the fields that were changed, capturing their
    /// values before the modification. Used for update and settings change actions to enable
    /// before/after comparison in the detail view.
    /// </remarks>
    public string? OldValues { get; set; }

    /// <summary>
    /// Gets or sets the JSON-serialized new state of the entity after the action, if applicable.
    /// </summary>
    /// <remarks>
    /// Nullable. Contains a JSON representation of the fields that were changed, capturing their
    /// values after the modification. Used for update and settings change actions to enable
    /// before/after comparison in the detail view.
    /// </remarks>
    public string? NewValues { get; set; }

    /// <summary>
    /// Gets or sets the IP address of the client that initiated the action, if available.
    /// </summary>
    /// <remarks>
    /// Nullable. Maximum length: 45 characters (sufficient for IPv6 addresses).
    /// Captured primarily for authentication-related events (login success/failure).
    /// </remarks>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp indicating when the action was recorded.
    /// </summary>
    /// <remarks>
    /// Always stored in UTC. The database column has a default value of <c>GETUTCDATE()</c>
    /// as a fallback, but the application explicitly sets this to <see cref="DateTime.UtcNow"/>
    /// at the time the audit entry is created by the service.
    /// </remarks>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the <see cref="ApplicationUser"/> who performed the action.
    /// </summary>
    /// <remarks>
    /// Nullable because the referenced user may have been deleted (FK uses restrict delete,
    /// but the user could still be removed if no audit entries reference them, or via direct DB manipulation).
    /// Used for EF Core relationship configuration; not typically loaded in queries.
    /// </remarks>
    public ApplicationUser? User { get; set; }
}
