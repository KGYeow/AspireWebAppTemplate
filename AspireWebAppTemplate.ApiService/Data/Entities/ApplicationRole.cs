using Microsoft.AspNetCore.Identity;

namespace AspireWebAppTemplate.ApiService.Data.Entities;

/// <summary>
/// Extends <see cref="IdentityRole"/> with additional metadata for display,
/// auditing, and soft-deactivation — consistent with <see cref="ApplicationUser"/>.
/// </summary>
/// <remarks>
/// After adding or changing properties here, run an EF Core migration to update
/// the underlying AspNetRoles table so the schema remains in sync with the model.
/// </remarks>
public class ApplicationRole : IdentityRole
{
    /// <summary>
    /// A human-readable label shown in the UI (e.g., "System Administrator").
    /// Falls back to <see cref="IdentityRole.Name"/> if not set.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Describes the purpose or permissions scope of the role
    /// (e.g., "Full access to all system modules and user management").
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// When false, the role is treated as deactivated and should not
    /// be assignable to users, even if it still exists in the database.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// The UTC timestamp when this role was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The UTC timestamp when this role was last modified. Null if never updated.
    /// </summary>
    public DateTime? UpdatedUtc { get; set; }

    /// <summary>
    /// When true, the role is protected from deletion, deactivation, and renaming.
    /// System roles (e.g., "Admin", "User") ensure the application always has
    /// functioning administrative and default roles.
    /// </summary>
    public bool IsSystem { get; set; } = false;

    /// <summary>
    /// When true, the system prevents removing the last user assigned to this role.
    /// This ensures critical roles (e.g., "Admin") always have at least one user,
    /// preventing accidental lockout of all administrators.
    /// </summary>
    public bool RequiresMinimumUser { get; set; } = false;

    /// <summary>
    /// When true, this role is automatically assigned to new users during
    /// registration or provisioning (local or LDAP). Only one role should
    /// have this flag set to true at any time.
    /// </summary>
    public bool IsDefault { get; set; } = false;

    /// <summary>
    /// Determines the authority level of the role in the hierarchy.
    /// A higher value indicates higher authority. Used to enforce that
    /// lower-positioned users cannot modify higher-positioned users
    /// or assign roles above their own level.
    /// </summary>
    public int Position { get; set; } = 0;
}