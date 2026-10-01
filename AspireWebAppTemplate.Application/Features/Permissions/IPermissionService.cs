namespace AspireWebAppTemplate.Application.Features.Permissions;

/// <summary>
/// Defines the contract for the permission service that manages the resource-based
/// (<c>Module.Action</c>) authorization model. A role's effective access is the set of
/// permission keys granted to it via <c>RolePermission</c> records; a user's effective
/// permissions are the union of grants across all roles assigned to that user.
/// </summary>
/// <remarks>
/// Implementations should be registered as scoped services to align with the per-request
/// <c>DbContext</c> lifetime. The Admin role is treated as having immutable full access to
/// every permission regardless of database records and must not be modified through this service.
/// </remarks>
public interface IPermissionService
{
    #region Query Operations

    /// <summary>
    /// Retrieves every defined permission grouped by its owning module, so the management UI
    /// can render the permission matrix with one row group per module.
    /// </summary>
    /// <returns>
    /// A task that resolves to a list of <see cref="PermissionGroupDto"/> objects, one per module,
    /// each containing the permissions that belong to that module.
    /// </returns>
    Task<List<PermissionGroupDto>> GetAllPermissionsGroupedAsync();

    /// <summary>
    /// Retrieves the list of permission keys currently granted to the specified role.
    /// </summary>
    /// <param name="roleId">
    /// The unique identifier of the role whose granted permission keys are being queried.
    /// </param>
    /// <returns>
    /// A task that resolves to the list of permission keys granted to the role.
    /// Returns an empty list if the role has no grants.
    /// </returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no role exists with the specified <paramref name="roleId"/>.
    /// </exception>
    Task<List<string>> GetRolePermissionKeysAsync(string roleId);

    /// <summary>
    /// Resolves the distinct union of permission keys granted across the specified set of roles.
    /// Used by the authorization pipeline to compute the effective permissions for a request.
    /// </summary>
    /// <param name="roleIds">
    /// The role identifiers whose granted permissions are combined. May be empty.
    /// </param>
    /// <returns>
    /// A task that resolves to the distinct list of permission keys granted to any of the
    /// supplied roles. Returns an empty list when <paramref name="roleIds"/> is empty or none
    /// of the roles have grants.
    /// </returns>
    Task<List<string>> GetPermissionsForRolesAsync(IEnumerable<string> roleIds);

    /// <summary>
    /// Retrieves the effective permission keys for the specified user, computed as the union
    /// of all permissions granted to the roles assigned to that user.
    /// </summary>
    /// <param name="userId">
    /// The unique identifier of the authenticated user whose effective permissions are queried.
    /// </param>
    /// <returns>
    /// A task that resolves to the distinct list of permission keys the user effectively holds.
    /// Returns an empty list if the user has no assigned roles or no permissions are granted.
    /// </returns>
    Task<List<string>> GetMyPermissionsAsync(string userId);

    /// <summary>
    /// Retrieves the static mapping of admin page paths to the module that gates each page.
    /// Used by the Web project to drive module-based page visibility and access checks.
    /// </summary>
    /// <returns>
    /// A task that resolves to the list of <see cref="PageModuleMappingDto"/> entries describing
    /// each known page-path to module association.
    /// </returns>
    Task<List<PageModuleMappingDto>> GetPageModuleMappingsAsync();

    #endregion

    #region Write Operations

    /// <summary>
    /// Replaces all existing permission grants for the specified role with the provided list of
    /// permission keys, applying a full-replacement strategy. An empty list removes all grants
    /// for the role.
    /// </summary>
    /// <param name="roleId">
    /// The unique identifier of the role whose permissions are being updated. Must correspond to
    /// an existing role and must not be the Admin role (which has immutable full access).
    /// Non-Admin system roles are permitted.
    /// </param>
    /// <param name="permissionKeys">
    /// The complete list of permission keys to grant to the role. Every key must correspond to a
    /// defined permission.
    /// </param>
    /// <returns>A task representing the asynchronous update operation.</returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no role exists with the specified <paramref name="roleId"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the specified role is the Admin role, whose full access cannot be modified.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when one or more of the supplied permission keys does not correspond to a
    /// defined permission.
    /// </exception>
    Task UpdateRolePermissionsAsync(string roleId, List<string> permissionKeys);

    #endregion
}
