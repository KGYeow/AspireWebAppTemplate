namespace AspireWebAppTemplate.Core.Contracts.PagePermissions;

/// <summary>
/// Request payload for updating the page permissions of a specific role.
/// Sent to the PUT /api/page-permissions/{roleId} endpoint.
/// The provided list of page paths fully replaces all existing permissions for the role.
/// </summary>
/// <param name="PagePaths">
/// The complete list of page paths to grant access to for the role.
/// An empty list removes all page permissions for the role.
/// Each path must start with "/" and match a page registered in the DefaultNavigationProvider.
/// </param>
public record UpdateRolePermissionsRequest(List<string> PagePaths);
