namespace AspireWebAppTemplate.Infrastructure.Data.Entities;

/// <summary>
/// Represents a single permission definition in the resource-based authorization model.
/// A permission is identified by a structured <see cref="Key"/> in <c>Module.Action</c> format
/// (e.g., "Users.Read") and is granted to roles via <see cref="RolePermission"/> join records.
/// </summary>
/// <remarks>
/// Stored in the "Permissions" table. The <see cref="Key"/> carries a unique index so that
/// each permission is defined exactly once. Roles accumulate permissions through
/// <see cref="RolePermission"/> entries; a user's effective permissions are the union of the
/// permissions granted to all of their roles.
/// </remarks>
public class Permission
{
    /// <summary>
    /// Gets or sets the unique identifier for this permission record.
    /// </summary>
    /// <remarks>
    /// Primary key, auto-incremented by the database.
    /// </remarks>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the structured permission key in <c>Module.Action</c> format (e.g., "Users.Read").
    /// </summary>
    /// <remarks>
    /// Required and unique. Maximum length: 100 characters. The segment before the dot is the
    /// module name and the segment after is the action; authorization policies and module-based
    /// page visibility are evaluated against this value.
    /// </remarks>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-readable display name of the permission.
    /// </summary>
    /// <remarks>
    /// Required. Maximum length: 200 characters. Used as the row label in the admin permission
    /// matrix UI (e.g., "View Users").
    /// </remarks>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the module this permission belongs to (the segment before the dot in <see cref="Key"/>).
    /// </summary>
    /// <remarks>
    /// Required. Maximum length: 50 characters. Permissions are grouped by module in the admin UI,
    /// and a role's membership in a module (holding any permission with this module prefix)
    /// determines visibility of the corresponding admin page.
    /// </remarks>
    public string Module { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional human-readable description of what the permission grants.
    /// </summary>
    /// <remarks>
    /// Nullable. Maximum length: 500 characters.
    /// </remarks>
    public string? Description { get; set; }
}
