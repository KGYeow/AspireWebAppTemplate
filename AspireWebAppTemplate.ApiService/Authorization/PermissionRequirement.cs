using Microsoft.AspNetCore.Authorization;

namespace AspireWebAppTemplate.ApiService.Authorization;

/// <summary>
/// Authorization requirement that carries the permission key a user must hold to be granted access.
/// Permission keys follow the resource-based <c>Module.Action</c> convention (e.g. <c>Users.Read</c>).
/// The <see cref="PermissionAuthorizationHandler"/> evaluates this requirement against the user's
/// effective permissions, and <see cref="PermissionPolicyProvider"/> builds a policy carrying this
/// requirement for any permission-key policy name encountered on an endpoint.
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionRequirement"/> class with the
    /// permission key that must be satisfied.
    /// </summary>
    /// <param name="permissionKey">The required permission key in <c>Module.Action</c> form (e.g. <c>Users.Read</c>).</param>
    public PermissionRequirement(string permissionKey)
    {
        PermissionKey = permissionKey;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the permission key the user must hold for this requirement to be satisfied.
    /// </summary>
    public string PermissionKey { get; }

    #endregion
}
