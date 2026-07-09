using AspireWebAppTemplate.ApiService.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AspireWebAppTemplate.ApiService.Data.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="AuditLogEntry"/> entity.
/// Defines table mapping, column constraints, indexes, and relationships.
/// </summary>
public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("AuditLogEntries");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserId).HasMaxLength(450);
        builder.Property(e => e.UserDisplayName).HasMaxLength(256);

        // Store enums as PascalCase strings for readability in raw SQL queries
        // and to prevent data corruption if enum integer values are reordered in the future.
        builder.Property(e => e.ActionType).HasConversion<string>();
        builder.Property(e => e.EntityType).HasConversion<string>();

        builder.Property(e => e.EntityId).HasMaxLength(450);
        builder.Property(e => e.EntityName).HasMaxLength(256);
        builder.Property(e => e.Description).HasMaxLength(1024);
        builder.Property(e => e.IpAddress).HasMaxLength(45);

        // Default to current UTC time at the database level as a safety net;
        // the application service also explicitly sets Timestamp on creation.
        builder.Property(e => e.Timestamp).HasDefaultValueSql("GETUTCDATE()");

        // Index on Timestamp supports efficient sorting and date range queries (newest-first default sort).
        builder.HasIndex(e => e.Timestamp);

        // Index on UserId supports filtering audit entries by a specific user.
        builder.HasIndex(e => e.UserId);

        // Index on ActionType supports filtering by action category (e.g., all logins).
        builder.HasIndex(e => e.ActionType);

        // Restrict delete prevents accidental cascade deletion of audit history
        // when a user is removed. Audit records must be preserved for compliance
        // regardless of the user's existence in the system.
        builder.HasOne(e => e.User)
              .WithMany()
              .HasForeignKey(e => e.UserId)
              .OnDelete(DeleteBehavior.Restrict);
    }
}
