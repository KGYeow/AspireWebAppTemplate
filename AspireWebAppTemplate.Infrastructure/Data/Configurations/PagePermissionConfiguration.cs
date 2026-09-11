using AspireWebAppTemplate.Infrastructure.Data.Entities;
using AspireWebAppTemplate.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AspireWebAppTemplate.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="PagePermission"/> entity.
/// Defines table mapping, column constraints, indexes, and relationships.
/// </summary>
public class PagePermissionConfiguration : IEntityTypeConfiguration<PagePermission>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PagePermission> builder)
    {
        builder.ToTable("PagePermissions");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.RoleId).IsRequired().HasMaxLength(450);
        builder.Property(e => e.PagePath).IsRequired().HasMaxLength(256);
        builder.Property(e => e.PageDisplayName).IsRequired().HasMaxLength(256);

        // Unique composite index on (RoleId, PagePath) prevents duplicate permission
        // grants for the same role-page combination and enforces the whitelist model's
        // invariant that at most one record exists per role-page pair.
        builder.HasIndex(e => new { e.RoleId, e.PagePath }).IsUnique();

        // Cascade delete ensures that when a role is removed from the system,
        // all associated page permission grants are automatically cleaned up,
        // preventing orphaned records that reference a non-existent role.
        builder.HasOne(e => e.Role)
              .WithMany()
              .HasForeignKey(e => e.RoleId)
              .OnDelete(DeleteBehavior.Cascade);
    }
}
