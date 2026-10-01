namespace AspireWebAppTemplate.Application.Features.Permissions;

/// <summary>
/// Request payload for updating the permissions granted to a specific role.
/// Sent to the PUT permission management endpoint.
/// The provided list of permission keys fully replaces all existing permissions for the role.
/// </summary>
public sealed class UpdateRolePermissionsRequest
{
    /// <summary>
    /// The complete list of permission keys (in <c>Module.Action</c> format) to grant to the role.
    /// An empty list removes all permissions from the role.
    /// Each key must match an existing permission definition.
    /// </summary>
    public List<string> PermissionKeys { get; set; } = [];
}
