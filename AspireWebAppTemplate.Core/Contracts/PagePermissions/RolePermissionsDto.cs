namespace AspireWebAppTemplate.Core.Contracts.PagePermissions;

/// <summary>
/// Represents the page permissions granted to a specific role.
/// Returned by the GET /api/page-permissions endpoint, grouping all page grants by role.
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
    /// The list of pages the role has been granted access to.
    /// </summary>
    public List<PagePermissionDto> Pages { get; set; } = [];
}
