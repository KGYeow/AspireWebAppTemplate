namespace AspireWebAppTemplate.Application.Contracts.PagePermissions;

/// <summary>
/// Request payload for updating the page permissions of a specific role.
/// Sent to the PUT /api/page-permissions/{roleId} endpoint.
/// The provided list of page paths fully replaces all existing permissions for the role.
/// </summary>
public sealed class UpdateRolePermissionsRequest
{
    /// <summary>
    /// The complete list of page paths to grant access to for the role.
    /// An empty list removes all page permissions for the role.
    /// Each path must start with "/" and match a page registered in the DefaultNavigationProvider.
    /// </summary>
    public List<string> PagePaths { get; set; } = [];
}
