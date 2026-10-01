using AspireWebAppTemplate.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AspireWebAppTemplate.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="RolePermission"/> join entity.
/// Defines table mapping, the composite primary key, column constraints, and the cascade-delete
/// relationships to the role and permission tables.
/// </summary>
public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");

        // Composite primary key on (RoleId, PermissionId) uniquely identifies each grant and
        // inherently prevents a role from being granted the same permission more than once.
        builder.HasKey(e => new { e.RoleId, e.PermissionId });

        builder.Property(e => e.RoleId).IsRequired().HasMaxLength(450);

        // Cascade delete on both foreign keys keeps the join table consistent: deleting a role
        // (ApplicationRoles) or a permission (Permissions) automatically removes the grant records
        // that reference it, preventing orphaned rows that would point at a non-existent role or
        // permission.
        builder.HasOne(e => e.Role)
              .WithMany()
              .HasForeignKey(e => e.RoleId)
              .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Permission)
              .WithMany()
              .HasForeignKey(e => e.PermissionId)
              .OnDelete(DeleteBehavior.Cascade);
    }
}
