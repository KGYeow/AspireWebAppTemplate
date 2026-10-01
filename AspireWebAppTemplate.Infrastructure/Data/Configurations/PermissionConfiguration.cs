using AspireWebAppTemplate.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AspireWebAppTemplate.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="Permission"/> entity.
/// Defines table mapping, column constraints, and the unique index on the permission key.
/// </summary>
public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Key).IsRequired().HasMaxLength(100);
        builder.Property(e => e.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Module).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Description).HasMaxLength(500);

        // Unique index on Key enforces that each permission is defined exactly once. The
        // permission key (e.g. "Users.Read") is the stable identifier used by authorization
        // policies, seed upserts, and role-permission grants, so duplicates would make
        // permission resolution and idempotent seeding ambiguous.
        builder.HasIndex(e => e.Key).IsUnique();
    }
}
