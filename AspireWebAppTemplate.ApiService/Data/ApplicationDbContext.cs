using AspireWebAppTemplate.ApiService.Data.Entities;
using AspireWebAppTemplate.Core.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AspireWebAppTemplate.ApiService.Data;

/// <summary>
/// Entity Framework Core DbContext for the application.
/// Manages user accounts, roles, identity-related data, and audit log entries.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
{
    /// <summary>
    /// Initializes a new instance of the ApplicationDbContext.
    /// </summary>
    /// <param name="options">The DbContext options.</param>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the audit log entries table, containing a record of all significant
    /// actions performed within the application.
    /// </summary>
    public DbSet<AuditLogEntry> AuditLogEntries { get; set; } = null!;

    /// <summary>
    /// Configures the entity mappings and table names for the database.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure ApplicationUser table
        modelBuilder.Entity<ApplicationUser>().ToTable("ApplicationUsers");
        modelBuilder.Entity<ApplicationUser>()
            .Property(u => u.AuthSource)
            .HasConversion<string>();
        modelBuilder.Entity<ApplicationUser>()
            .Property(u => u.Theme)
            .HasConversion<string>()
            .HasDefaultValue(ThemePreference.System);

        // Configure ApplicationRole table
        modelBuilder.Entity<ApplicationRole>().ToTable("ApplicationRoles");

        // Configure Identity tables
        modelBuilder.Entity<IdentityUserRole<string>>().ToTable("ApplicationUserRoles");
        modelBuilder.Entity<IdentityUserClaim<string>>().ToTable("ApplicationUserClaims");
        modelBuilder.Entity<IdentityRoleClaim<string>>().ToTable("ApplicationRoleClaims");
        modelBuilder.Entity<IdentityUserLogin<string>>().ToTable("ApplicationUserLogins");
        modelBuilder.Entity<IdentityUserToken<string>>().ToTable("ApplicationUserTokens");

        // Configure AuditLogEntry entity
        modelBuilder.Entity<AuditLogEntry>(entity =>
        {
            entity.ToTable("AuditLogEntries");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.UserId).HasMaxLength(450);
            entity.Property(e => e.UserDisplayName).HasMaxLength(256);

            // Store enums as PascalCase strings for readability in raw SQL queries
            // and to prevent data corruption if enum integer values are reordered in the future.
            entity.Property(e => e.ActionType).HasConversion<string>();
            entity.Property(e => e.EntityType).HasConversion<string>();

            entity.Property(e => e.EntityId).HasMaxLength(450);
            entity.Property(e => e.EntityName).HasMaxLength(256);
            entity.Property(e => e.Description).HasMaxLength(1024);
            entity.Property(e => e.IpAddress).HasMaxLength(45);

            // Default to current UTC time at the database level as a safety net;
            // the application service also explicitly sets Timestamp on creation.
            entity.Property(e => e.Timestamp).HasDefaultValueSql("GETUTCDATE()");

            // Index on Timestamp supports efficient sorting and date range queries (newest-first default sort).
            entity.HasIndex(e => e.Timestamp);

            // Index on UserId supports filtering audit entries by a specific user.
            entity.HasIndex(e => e.UserId);

            // Index on ActionType supports filtering by action category (e.g., all logins).
            entity.HasIndex(e => e.ActionType);

            // Restrict delete prevents accidental cascade deletion of audit history
            // when a user is removed. Audit records must be preserved for compliance
            // regardless of the user's existence in the system.
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
