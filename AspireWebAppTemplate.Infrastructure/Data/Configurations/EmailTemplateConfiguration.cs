using AspireWebAppTemplate.Domain.Entities.Template;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AspireWebAppTemplate.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="EmailTemplate"/> entity.
/// Defines table mapping, column constraints, indexes, and unique constraints.
/// </summary>
public class EmailTemplateConfiguration : IEntityTypeConfiguration<EmailTemplate>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<EmailTemplate> builder)
    {
        builder.ToTable("EmailTemplates");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Subject).IsRequired().HasMaxLength(500);
        builder.Property(e => e.HtmlBody).IsRequired();
        builder.Property(e => e.PlaceholderHints).HasMaxLength(1000);

        // Store enums as PascalCase strings for readability in raw SQL queries
        // and to prevent data corruption if enum integer values are reordered.
        builder.Property(e => e.Category).HasConversion<string>();
        builder.Property(e => e.EmailType).HasConversion<string>();

        // Unique constraint on EmailType ensures exactly one template per
        // EmailType. This is the core invariant of the edit-only model —
        // application code can always resolve a single template by enum value.
        builder.HasIndex(e => e.EmailType)
              .IsUnique()
              .HasDatabaseName("IX_EmailTemplates_EmailType");

        // Index on Category supports efficient filtering in the admin list
        // (e.g., showing only business templates for editing).
        builder.HasIndex(e => e.Category)
              .HasDatabaseName("IX_EmailTemplates_Category");
    }
}
