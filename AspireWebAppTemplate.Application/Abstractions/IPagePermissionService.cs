using AspireWebAppTemplate.Application.Contracts.PagePermissions;

namespace AspireWebAppTemplate.Application.Abstractions;

/// <summary>
/// Defines the contract for the page permission service that manages role-based page access
/// grants using a whitelist model. A <see cref="RolePermissionsDto"/> record existing for a
/// role-page combination grants access; absence denies it.
/// </summary>
/// <remarks>
/// Implementations should be registered as scoped services to align with the per-request
/// <c>DbContext</c> lifetime. The Admin role is treated as having immutable full access
/// to all pages regardless of database records.
/// </remarks>
public interface IPagePermissionService
{
    #region Query Operations

    /// <summary>
    /// Retrieves all page permission records grouped by role, including each role's
    /// identifier, display name, and the list of granted page paths with their display names.
    /// </summary>
    /// <returns>
    /// A task that resolves to a list of <see cref="RolePermissionsDto"/> objects,
    /// one per role that has at least one page permission grant.
    /// </returns>
    Task<List<RolePermissionsDto>> GetAllPermissionsAsync();

    /// <summary>
    /// Retrieves the list of page paths accessible to the specified user based on the
    /// union of all page permissions across all roles assigned to that user.
    /// </summary>
    /// <param name="userId">
    /// The unique identifier of the authenticated user whose accessible pages are being queried.
    /// </param>
    /// <returns>
    /// A task that resolves to a list of page path strings the user is permitted to access.
    /// Returns an empty list if the user has no assigned roles or no permissions are granted.
    /// </returns>
    Task<List<string>> GetMyPagesAsync(string userId);

    #endregion

    #region Write Operations

    /// <summary>
    /// Replaces all existing page permission records for the specified role with the provided
    /// list of page paths. An empty list removes all page permissions for that role.
    /// </summary>
    /// <param name="roleId">
    /// The unique identifier of the role whose permissions are being updated.
    /// Must correspond to an existing role in AspNetRoles and must not be the Admin role
    /// or a system role.
    /// </param>
    /// <param name="pagePaths">
    /// The complete list of page paths to grant to the role. Each path must start with "/"
    /// and match a page registered in the DefaultNavigationProvider.
    /// </param>
    /// <returns>A task representing the asynchronous update operation.</returns>
    Task UpdateRolePermissionsAsync(string roleId, List<string> pagePaths);

    #endregion
}
