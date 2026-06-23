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
    /// Gets or sets the page permissions table, storing role-to-page access grants
    /// for the database-driven authorization system.
    /// </summary>
    public DbSet<PagePermission> PagePermissions { get; set; } = null!;

    /// <summary>
    /// Gets or sets the notifications table, containing in-app notification records
    /// delivered to users based on significant system events.
    /// </summary>
    public DbSet<Notification> Notifications { get; set; } = null!;

    /// <summary>
    /// Gets or sets the notification preferences table, storing per-user delivery
    /// channel preferences for each notification category.
    /// </summary>
    public DbSet<NotificationPreference> NotificationPreferences { get; set; } = null!;

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

        // Configure PagePermission entity
        modelBuilder.Entity<PagePermission>(entity =>
        {
            entity.ToTable("PagePermissions");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.RoleId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.PagePath).IsRequired().HasMaxLength(256);
            entity.Property(e => e.PageDisplayName).IsRequired().HasMaxLength(256);

            // Unique composite index on (RoleId, PagePath) prevents duplicate permission
            // grants for the same role-page combination and enforces the whitelist model's
            // invariant that at most one record exists per role-page pair.
            entity.HasIndex(e => new { e.RoleId, e.PagePath }).IsUnique();

            // Cascade delete ensures that when a role is removed from the system,
            // all associated page permission grants are automatically cleaned up,
            // preventing orphaned records that reference a non-existent role.
            entity.HasOne(e => e.Role)
                  .WithMany()
                  .HasForeignKey(e => e.RoleId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Notification entity
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notifications");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.UserId).HasMaxLength(450);

            // Store enum as PascalCase string for readability in raw SQL queries
            // and to prevent data corruption if enum integer values are reordered in the future.
            entity.Property(e => e.Category).HasConversion<string>();

            entity.Property(e => e.Title).HasMaxLength(256);
            entity.Property(e => e.Message).HasMaxLength(1024);

            // Default to unread — new notifications are always created as unread.
            entity.Property(e => e.IsRead).HasDefaultValue(false);

            // Default to current UTC time at the database level as a safety net;
            // the application service also explicitly sets CreatedAtUtc on creation.
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("GETUTCDATE()");

            // Composite index on (UserId, IsRead) enables efficient unread count queries
            // by allowing the database to seek directly to a user's unread notifications
            // without scanning the entire table.
            entity.HasIndex(e => new { e.UserId, e.IsRead });

            // Composite index on (UserId, CreatedAtUtc) supports efficient paginated
            // retrieval of a user's notifications in descending chronological order,
            // which is the default sort for the notification page and bell dropdown.
            entity.HasIndex(e => new { e.UserId, e.CreatedAtUtc });

            // Cascade delete ensures that when a user is removed from the system,
            // all their notification records are automatically cleaned up.
            // Unlike audit log entries (which must be preserved for compliance),
            // notifications have no retention requirement beyond the user's lifetime.
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure NotificationPreference entity
        modelBuilder.Entity<NotificationPreference>(entity =>
        {
            entity.ToTable("NotificationPreferences");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.UserId).HasMaxLength(450);

            // Store enum as PascalCase string for readability in raw SQL queries
            // and to prevent data corruption if enum integer values are reordered in the future.
            entity.Property(e => e.Category).HasConversion<string>();

            // Unique composite index on (UserId, Category) enforces the business rule
            // that at most one preference record exists per user-category pair.
            // This prevents duplicate preferences and allows upsert logic in the service.
            entity.HasIndex(e => new { e.UserId, e.Category }).IsUnique();

            // Cascade delete ensures that when a user is removed from the system,
            // all their notification preference records are automatically cleaned up.
            // Preferences are user-specific configuration with no value beyond the user's lifetime.
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
