using AspireWebAppTemplate.ApiService.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AspireWebAppTemplate.ApiService.Data.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="AnnouncementDismissal"/> entity.
/// Defines table mapping, composite primary key, column constraints, and relationships.
/// </summary>
public class AnnouncementDismissalConfiguration : IEntityTypeConfiguration<AnnouncementDismissal>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AnnouncementDismissal> builder)
    {
        builder.ToTable("AnnouncementDismissals");

        // Composite primary key on (UserId, AnnouncementId) enforces the business rule
        // that each user can dismiss a given announcement at most once, making the
        // dismiss operation naturally idempotent at the database level.
        builder.HasKey(e => new { e.UserId, e.AnnouncementId });

        builder.Property(e => e.UserId).HasMaxLength(450);

        // Cascade delete ensures that when a user is removed from the system,
        // all their dismissal records are automatically cleaned up.
        // Dismissals are user-specific state with no value beyond the user's lifetime.
        builder.HasOne(e => e.User)
              .WithMany()
              .HasForeignKey(e => e.UserId)
              .OnDelete(DeleteBehavior.Cascade);
    }
}
