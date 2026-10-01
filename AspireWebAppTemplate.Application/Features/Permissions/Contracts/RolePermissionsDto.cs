namespace AspireWebAppTemplate.Application.Features.Permissions;

/// <summary>
/// Represents the permission keys granted to a specific role in the resource-based authorization model.
/// Returned by the GET by-role permission endpoint.
/// </summary>
public sealed class RolePermissionsDto
{
    /// <summary>
    /// The unique identifier of the role.
    /// </summary>
    public string RoleId { get; set; } = "";

    /// <summary>
    /// The display name of the role.
    /// </summary>
    public string RoleName { get; set; } = "";

    /// <summary>
    /// The list of permission keys (in <c>Module.Action</c> format) the role has been granted.
    /// </summary>
    public List<string> PermissionKeys { get; set; } = [];
}
