namespace AspireWebAppTemplate.Application.Features.Permissions;

/// <summary>
/// Represents a set of permissions grouped by their owning module.
/// Returned by the GET all-permissions endpoint so the management UI can render permissions by module.
/// </summary>
public sealed class PermissionGroupDto
{
    /// <summary>
    /// The module name that owns the grouped permissions (e.g. "Users").
    /// </summary>
    public string Module { get; set; } = "";

    /// <summary>
    /// The list of permissions belonging to this module.
    /// </summary>
    public List<PermissionDto> Permissions { get; set; } = [];
}
