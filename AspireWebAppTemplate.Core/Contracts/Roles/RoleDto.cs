namespace AspireWebAppTemplate.Core.Contracts.Roles;

/// <summary>
/// Data transfer object representing a role in the system.
/// Returned by role management API endpoints.
/// </summary>
public sealed class RoleDto
{
    /// <summary>
    /// The unique identifier of the role.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// The unique name identifier for the role.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// The human-friendly display name for the role.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// A description explaining the role's purpose.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether the role is active and can be assigned to users.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Whether this is a built-in system role that cannot be deleted.
    /// </summary>
    public bool IsSystem { get; set; }

    /// <summary>
    /// Whether this role is automatically assigned to new users.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Whether the system requires at least one user to hold this role.
    /// </summary>
    public bool RequiresMinimumUser { get; set; }

    /// <summary>
    /// The sort order position for display purposes.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// The number of users currently assigned to this role.
    /// </summary>
    public int UserCount { get; set; }

    /// <summary>
    /// The UTC timestamp when the role was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// The UTC timestamp when the role was last updated.
    /// </summary>
    public DateTime? UpdatedUtc { get; set; }
}
