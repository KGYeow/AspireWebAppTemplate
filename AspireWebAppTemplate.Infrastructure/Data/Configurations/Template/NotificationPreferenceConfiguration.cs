using AspireWebAppTemplate.Infrastructure.Data.Entities.Template;
using AspireWebAppTemplate.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AspireWebAppTemplate.Infrastructure.Data.Configurations.Template;

/// <summary>
/// EF Core configuration for the <see cref="NotificationPreference"/> entity.
/// Defines table mapping, column constraints, indexes, and relationships.
/// </summary>
public class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("NotificationPreferences");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserId).HasMaxLength(450);

        // Store enum as PascalCase string for readability in raw SQL queries
        // and to prevent data corruption if enum integer values are reordered in the future.
        builder.Property(e => e.Category).HasConversion<string>();

        // Unique composite index on (UserId, Category) enforces the business rule
        // that at most one preference record exists per user-category pair.
        // This prevents duplicate preferences and allows upsert logic in the service.
        builder.HasIndex(e => new { e.UserId, e.Category }).IsUnique();

        // Cascade delete ensures that when a user is removed from the system,
        // all their notification preference records are automatically cleaned up.
        // Preferences are user-specific configuration with no value beyond the user's lifetime.
        builder.HasOne(e => e.User)
              .WithMany()
              .HasForeignKey(e => e.UserId)
              .OnDelete(DeleteBehavior.Cascade);
    }
}
