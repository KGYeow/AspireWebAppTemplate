namespace AspireWebAppTemplate.Core.Contracts.PagePermissions;

/// <summary>
/// Represents the page permissions granted to a specific role.
/// Returned by the GET /api/page-permissions endpoint, grouping all page grants by role.
/// </summary>
/// <param name="RoleId">The unique identifier of the role.</param>
/// <param name="RoleName">The display name of the role.</param>
/// <param name="Pages">The list of pages the role has been granted access to.</param>
public record RolePermissionsDto(string RoleId, string RoleName, List<PagePermissionDto> Pages);
