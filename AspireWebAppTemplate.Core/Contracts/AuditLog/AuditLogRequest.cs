using AspireWebAppTemplate.Core.Domain.Enums;

namespace AspireWebAppTemplate.Core.Contracts.AuditLog;

/// <summary>
/// Encapsulates all parameters for recording a single audit log entry.
/// Replaces the long-parameter-list <c>LogAsync</c> method signature with a single
/// strongly-typed DTO, improving readability, extensibility, and testability.
/// </summary>
public sealed class AuditLogRequest
{
    /// <summary>
    /// The identifier of the acting user, or <c>null</c> for system-generated events
    /// where no authenticated user context exists.
    /// </summary>
    /// <remarks>
    /// When non-null, the <c>AuditLogService</c> resolves the user's display name via
    /// <c>UserManager.FindByIdAsync</c>. When null, an empty string is stored as the
    /// <c>UserDisplayName</c> on the persisted entry.
    /// </remarks>
    public string? UserId { get; set; }

    /// <summary>
    /// The category of action being recorded (e.g., UserCreated, LoginSuccess, SettingsChanged).
    /// </summary>
    public AuditActionType ActionType { get; set; }

    /// <summary>
    /// The type of entity affected by the action (e.g., User, Role, Settings).
    /// </summary>
    public AuditEntityType EntityType { get; set; }

    /// <summary>
    /// The unique identifier of the affected entity (e.g., user ID, role ID).
    /// Maximum 450 characters when persisted.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="string.Empty"/> to match the previous method signature behavior.
    /// </remarks>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>
    /// A human-readable name for the affected entity (e.g., user display name, role name).
    /// Maximum 256 characters when persisted.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="string.Empty"/> to match the previous method signature behavior.
    /// </remarks>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>
    /// A brief human-readable summary of what occurred.
    /// Maximum 1024 characters when persisted.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="string.Empty"/> to match the previous method signature behavior.
    /// </remarks>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// JSON-serialized representation of the entity's previous state before the action,
    /// or <c>null</c> if not applicable (e.g., for creation, login, or delete events).
    /// </summary>
    /// <remarks>
    /// Only changed fields are included in the JSON payload, serialized with camelCase
    /// property naming via <see cref="System.Text.Json.JsonNamingPolicy.CamelCase"/>.
    /// Null property values are preserved as JSON <c>null</c> rather than omitted.
    /// </remarks>
    public string? OldValues { get; set; }

    /// <summary>
    /// JSON-serialized representation of the entity's new state after the action,
    /// or <c>null</c> if not applicable (e.g., for deletion or login events).
    /// </summary>
    /// <remarks>
    /// Only changed fields are included in the JSON payload, serialized with camelCase
    /// property naming via <see cref="System.Text.Json.JsonNamingPolicy.CamelCase"/>.
    /// Null property values are preserved as JSON <c>null</c> rather than omitted.
    /// </remarks>
    public string? NewValues { get; set; }

    /// <summary>
    /// The source IP address of the HTTP request that triggered the action,
    /// or <c>null</c> if not available. Maximum 45 characters (supports IPv6).
    /// </summary>
    public string? IpAddress { get; set; }
}
