using AspireWebAppTemplate.ApiService.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AspireWebAppTemplate.ApiService.Data.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="Announcement"/> entity.
/// Defines table mapping, column constraints, indexes, and relationships.
/// </summary>
public class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.ToTable("Announcements");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Title).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Content).IsRequired().HasMaxLength(10000);

        // Store enums as PascalCase strings for readability in raw SQL queries
        // and to prevent data corruption if enum integer values are reordered in the future.
        builder.Property(e => e.DisplayType).HasConversion<string>();
        builder.Property(e => e.Severity).HasConversion<string>();

        builder.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(450);

        // Composite index on (IsActive, StartsAtUtc, ExpiresAtUtc) enables efficient
        // active announcement queries by allowing the database to seek directly to
        // announcements that are active and within their scheduling window, avoiding
        // a full table scan on every page load.
        builder.HasIndex(e => new { e.IsActive, e.StartsAtUtc, e.ExpiresAtUtc })
              .HasDatabaseName("IX_Announcements_IsActive_StartsAtUtc_ExpiresAtUtc");

        // Index on CreatedAtUtc supports efficient ordering by creation date (newest first),
        // which is the default sort for admin lists, dashboard cards, and priority tie-breaking.
        builder.HasIndex(e => e.CreatedAtUtc)
              .HasDatabaseName("IX_Announcements_CreatedAtUtc");

        // Restrict delete prevents cascade deletion of announcements when an admin user
        // is removed from the system. Announcements must be preserved for historical
        // visibility even if the creator account is deleted.
        builder.HasOne(e => e.CreatedByUser)
              .WithMany()
              .HasForeignKey(e => e.CreatedByUserId)
              .OnDelete(DeleteBehavior.Restrict);

        // Cascade delete ensures that when an announcement is removed,
        // all per-user dismissal records for that announcement are automatically cleaned up.
        builder.HasMany(e => e.Dismissals)
              .WithOne(e => e.Announcement)
              .HasForeignKey(e => e.AnnouncementId)
              .OnDelete(DeleteBehavior.Cascade);
    }
}
