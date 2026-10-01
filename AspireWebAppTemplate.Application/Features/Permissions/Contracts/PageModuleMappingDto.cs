namespace AspireWebAppTemplate.Application.Features.Permissions;

/// <summary>
/// Represents a single entry in the static page-path to module mapping.
/// Used by the Web project to determine which module gates visibility and access for an admin page.
/// </summary>
public sealed class PageModuleMappingDto
{
    /// <summary>
    /// The admin page path (e.g. "/admin/user-management").
    /// </summary>
    public string PagePath { get; set; } = "";

    /// <summary>
    /// The module that gates access to the page (e.g. "Users").
    /// </summary>
    public string Module { get; set; } = "";
}
