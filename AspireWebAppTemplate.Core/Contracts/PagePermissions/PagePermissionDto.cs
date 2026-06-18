namespace AspireWebAppTemplate.Core.Contracts.PagePermissions;

/// <summary>
/// Represents a single page permission grant, containing the page's route path
/// and its human-readable display name.
/// </summary>
/// <param name="PagePath">The route path of the page (e.g., "/admin/audit-log"). Must start with "/".</param>
/// <param name="PageDisplayName">The human-readable display name of the page (e.g., "Audit Log").</param>
public record PagePermissionDto(string PagePath, string PageDisplayName);
