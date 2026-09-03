using AspireWebAppTemplate.Infrastructure.Data.Entities.Template;
using AspireWebAppTemplate.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AspireWebAppTemplate.Infrastructure.Data.Configurations.Template;

/// <summary>
/// EF Core configuration for the <see cref="Notification"/> entity.
/// Defines table mapping, column constraints, indexes, and relationships.
/// </summary>
public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserId).HasMaxLength(450);

        // Store enum as PascalCase string for readability in raw SQL queries
        // and to prevent data corruption if enum integer values are reordered in the future.
        builder.Property(e => e.Category).HasConversion<string>();

        builder.Property(e => e.Title).HasMaxLength(256);
        builder.Property(e => e.Message).HasMaxLength(1024);

        // Default to unread — new notifications are always created as unread.
        builder.Property(e => e.IsRead).HasDefaultValue(false);

        // Default to current UTC time at the database level as a safety net;
        // the application service also explicitly sets CreatedAtUtc on creation.
        builder.Property(e => e.CreatedAtUtc).HasDefaultValueSql("GETUTCDATE()");

        // Composite index on (UserId, IsRead) enables efficient unread count queries
        // by allowing the database to seek directly to a user's unread notifications
        // without scanning the entire table.
        builder.HasIndex(e => new { e.UserId, e.IsRead });

        // Composite index on (UserId, CreatedAtUtc) supports efficient paginated
        // retrieval of a user's notifications in descending chronological order,
        // which is the default sort for the notification page and bell dropdown.
        builder.HasIndex(e => new { e.UserId, e.CreatedAtUtc });

        // Cascade delete ensures that when a user is removed from the system,
        // all their notification records are automatically cleaned up.
        // Unlike audit log entries (which must be preserved for compliance),
        // notifications have no retention requirement beyond the user's lifetime.
        builder.HasOne(e => e.User)
              .WithMany()
              .HasForeignKey(e => e.UserId)
              .OnDelete(DeleteBehavior.Cascade);
    }
}
