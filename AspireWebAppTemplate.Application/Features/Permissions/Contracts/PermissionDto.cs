namespace AspireWebAppTemplate.Application.Features.Permissions;

/// <summary>
/// Represents a single permission definition in the resource-based authorization model.
/// Returned by the permission management API to describe an available permission grant.
/// </summary>
public sealed class PermissionDto
{
    /// <summary>
    /// The unique identifier of the permission definition.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The structured permission key in <c>Module.Action</c> format (e.g. "Users.Read"). Unique across all permissions.
    /// </summary>
    public string Key { get; set; } = "";

    /// <summary>
    /// The human-readable display name of the permission (e.g. "View Users").
    /// </summary>
    public string DisplayName { get; set; } = "";

    /// <summary>
    /// An optional free-text description explaining what the permission grants.
    /// </summary>
    public string? Description { get; set; }
}
