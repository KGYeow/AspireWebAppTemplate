using AspireWebAppTemplate.Infrastructure.Identity;

namespace AspireWebAppTemplate.Infrastructure.Data.Entities;

/// <summary>
/// Represents a join record granting a single <see cref="Permission"/> to a single role in the
/// resource-based authorization model. The presence of a record means the role holds that
/// permission; its absence means the role does not.
/// </summary>
/// <remarks>
/// Stored in the "RolePermissions" table with a composite primary key on
/// (<see cref="RoleId"/>, <see cref="PermissionId"/>) which also prevents duplicate grants.
/// Both foreign keys use cascade delete so that removing a role or a permission automatically
/// removes the associated grant records.
/// </remarks>
public class RolePermission
{
    /// <summary>
    /// Gets or sets the identifier of the role this permission is granted to.
    /// </summary>
    /// <remarks>
    /// Foreign key referencing <c>ApplicationRoles.Id</c>. Maximum length: 450 characters.
    /// Part of the composite primary key with <see cref="PermissionId"/>.
    /// </remarks>
    public string RoleId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the identifier of the permission being granted to the role.
    /// </summary>
    /// <remarks>
    /// Foreign key referencing <c>Permissions.Id</c>. Part of the composite primary key with
    /// <see cref="RoleId"/>.
    /// </remarks>
    public int PermissionId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the <see cref="ApplicationRole"/> this grant belongs to.
    /// </summary>
    /// <remarks>
    /// Configured with cascade delete behavior so that removing a role automatically removes all of
    /// its permission grants.
    /// </remarks>
    public ApplicationRole Role { get; set; } = null!;

    /// <summary>
    /// Gets or sets the navigation property to the <see cref="Permission"/> this grant references.
    /// </summary>
    /// <remarks>
    /// Configured with cascade delete behavior so that removing a permission automatically removes all
    /// of the grants that reference it.
    /// </remarks>
    public Permission Permission { get; set; } = null!;
}
