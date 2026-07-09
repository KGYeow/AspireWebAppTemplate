using AspireWebAppTemplate.ApiService.Data.Entities;
using AspireWebAppTemplate.Core.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AspireWebAppTemplate.ApiService.Data;

/// <summary>
/// Entity Framework Core DbContext for the application.
/// Manages user accounts, roles, identity-related data, and all domain entities.
/// Entity configurations are defined in separate <see cref="IEntityTypeConfiguration{TEntity}"/>
/// classes under the <c>Data/Configurations/</c> folder for maintainability.
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

    #region Template

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
    /// Gets or sets the announcements table, containing system-wide announcement records
    /// that are displayed to users through banners, dashboard cards, and list pages.
    /// </summary>
    public DbSet<Announcement> Announcements { get; set; } = null!;

    /// <summary>
    /// Gets or sets the announcement dismissals table, tracking per-user dismissals
    /// of banner announcements so dismissed items are excluded from a user's banner view.
    /// </summary>
    public DbSet<AnnouncementDismissal> AnnouncementDismissals { get; set; } = null!;

    #endregion

    #region Custom

    // Add your application-specific DbSet properties below this line.
    // Example:
    // public DbSet<Order> Orders { get; set; } = null!;
    // public DbSet<Invoice> Invoices { get; set; } = null!;

    #endregion

    /// <summary>
    /// Configures the entity model using Identity table name overrides and
    /// <see cref="IEntityTypeConfiguration{TEntity}"/> classes discovered from this assembly.
    /// </summary>
    /// <remarks>
    /// Entity configurations are located in <c>Data/Configurations/</c>. To add configuration
    /// for a new entity, create a class implementing <c>IEntityTypeConfiguration&lt;T&gt;</c>
    /// in that folder — it will be automatically discovered and applied.
    /// </remarks>
    /// <param name="modelBuilder">The model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Identity table names
        modelBuilder.Entity<ApplicationUser>().ToTable("ApplicationUsers");
        modelBuilder.Entity<ApplicationUser>()
            .Property(u => u.AuthSource)
            .HasConversion<string>();
        modelBuilder.Entity<ApplicationUser>()
            .Property(u => u.Theme)
            .HasConversion<string>()
            .HasDefaultValue(ThemePreference.System);

        modelBuilder.Entity<ApplicationRole>().ToTable("ApplicationRoles");

        modelBuilder.Entity<IdentityUserRole<string>>().ToTable("ApplicationUserRoles");
        modelBuilder.Entity<IdentityUserClaim<string>>().ToTable("ApplicationUserClaims");
        modelBuilder.Entity<IdentityRoleClaim<string>>().ToTable("ApplicationRoleClaims");
        modelBuilder.Entity<IdentityUserLogin<string>>().ToTable("ApplicationUserLogins");
        modelBuilder.Entity<IdentityUserToken<string>>().ToTable("ApplicationUserTokens");

        // Apply all IEntityTypeConfiguration<T> classes from this assembly.
        // To configure a new entity, add a class in Data/Configurations/ implementing
        // IEntityTypeConfiguration<YourEntity> — it will be discovered automatically.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
