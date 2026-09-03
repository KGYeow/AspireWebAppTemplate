using AspireWebAppTemplate.Infrastructure.Identity;
namespace AspireWebAppTemplate.Infrastructure.Data.Entities.Template;

/// <summary>
/// Represents a single role-to-page access grant in the whitelist permission model.
/// A record existing for a given <see cref="RoleId"/> and <see cref="PagePath"/> means
/// that role is permitted to access that page; absence of a record means access is denied.
/// </summary>
/// <remarks>
/// Stored in the "PagePermissions" table with a unique composite index on
/// (<see cref="RoleId"/>, <see cref="PagePath"/>) to prevent duplicate grants.
/// The foreign key to <see cref="ApplicationRole"/> uses cascade delete so that
/// removing a role automatically removes all its associated page permissions.
/// </remarks>
public class PagePermission
{
    /// <summary>
    /// Gets or sets the unique identifier for this page permission record.
    /// </summary>
    /// <remarks>
    /// Primary key, auto-incremented by the database.
    /// </remarks>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the role this permission is granted to.
    /// </summary>
    /// <remarks>
    /// Foreign key referencing <c>ApplicationRoles.Id</c>. Maximum length: 450 characters.
    /// Combined with <see cref="PagePath"/> to form the unique composite index that
    /// prevents duplicate permission grants for the same role-page pair.
    /// </remarks>
    public string RoleId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the route path of the page this permission grants access to.
    /// </summary>
    /// <remarks>
    /// Must start with "/" and contain no query string or fragment (e.g., "/admin/audit-log").
    /// Maximum length: 256 characters. Permission lookups use case-insensitive ordinal comparison.
    /// </remarks>
    public string PagePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-readable display name of the page.
    /// </summary>
    /// <remarks>
    /// Used in the admin permission matrix UI as the row label.
    /// Maximum length: 256 characters. Sourced from <c>NavItem.Text</c> in the
    /// <c>DefaultNavigationProvider</c>.
    /// </remarks>
    public string PageDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the navigation property to the <see cref="ApplicationRole"/> this permission belongs to.
    /// </summary>
    /// <remarks>
    /// Configured with cascade delete behavior so that removing a role from the system
    /// automatically removes all of its page permission grants.
    /// </remarks>
    public ApplicationRole Role { get; set; } = null!;
}
