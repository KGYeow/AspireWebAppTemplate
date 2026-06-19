namespace AspireWebAppTemplate.Core.Contracts.PagePermissions;

/// <summary>
/// Represents a single page permission grant, containing the page's route path
/// and its human-readable display name.
/// </summary>
public sealed class PagePermissionDto
{
    /// <summary>
    /// The route path of the page (e.g., "/admin/audit-log"). Must start with "/".
    /// </summary>
    public string PagePath { get; set; } = "";

    /// <summary>
    /// The human-readable display name of the page (e.g., "Audit Log").
    /// </summary>
    public string PageDisplayName { get; set; } = "";
}
