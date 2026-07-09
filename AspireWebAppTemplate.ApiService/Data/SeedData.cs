using AspireWebAppTemplate.ApiService.Data.Entities;
using AspireWebAppTemplate.Core.Application.Abstractions;
using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Core.Common.Defaults;
using AspireWebAppTemplate.Core.Domain.Enums;
using AspireWebAppTemplate.Core.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AspireWebAppTemplate.ApiService.Data;

/// <summary>
/// Provides database seeding logic for roles and default user accounts.
/// Intended for development and initial deployment only.
/// </summary>
/// <remarks>
/// Called once on application startup (in Development) via <c>Program.cs</c>.
/// Seed credentials should be rotated or removed before going to production.
/// </remarks>
public static class SeedData
{
    /// <summary>
    /// Seed role definitions. Each entry maps to a real <see cref="ApplicationRole"/>
    /// that will be created if it does not already exist.
    /// </summary>
    private static readonly SeedRole[] SeedRoles =
    [
        new(
            Name:                "Admin",
            DisplayName:         "Administrator",
            Description:         "Full access to all system modules and user management.",
            IsSystem:            true,
            RequiresMinimumUser: true,
            IsDefault:           false,
            Position:            100
        ),
        new(
            Name:                "User",
            DisplayName:         "Regular User",
            Description:         "Standard access to general application features.",
            IsSystem:            true,
            RequiresMinimumUser: false,
            IsDefault:           true,
            Position:            10
        ),
    ];

    /// <summary>
    /// Seed user definitions. Each entry maps to a real <see cref="ApplicationUser"/>
    /// that will be created if it does not already exist.
    /// </summary>
    private static readonly SeedUser[] SeedUsers =
    [
        new(
            Email:        "admin@example.com",
            UserName:     "admin@example.com",
            FirstName:    "Admin",
            LastName:     "User",
            DisplayName:  "Administrator",
            Password:     "Admin123#",
            Role:         "Admin"
        ),
        new(
            Email:        "user@example.com",
            UserName:     "user@example.com",
            FirstName:    "Regular",
            LastName:     "User",
            DisplayName:  "Regular User",
            Password:     "User123#",
            Role:         "User"
        ),
    ];

    /// <summary>
    /// Entry point called from <c>Program.cs</c> to seed roles, users, and page permissions.
    /// Skips creation of any role or user that already exists.
    /// </summary>
    /// <param name="services">
    /// A scoped <see cref="IServiceProvider"/> from which Identity services are resolved.
    /// </param>
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        var navigationProvider = services.GetRequiredService<INavigationProvider>();
        var logger = services.GetRequiredService<ILogger<SeedMarker>>();

        await SeedRolesAsync(roleManager, logger);
        await SeedUsersAsync(userManager, logger);
        await SeedPagePermissionsAsync(dbContext, roleManager, navigationProvider, logger);
        await SeedAnnouncementsAsync(dbContext, userManager, logger);
    }

    /// <summary>
    /// Creates any roles in <see cref="SeedRoles"/> that do not yet exist,
    /// populated with <see cref="ApplicationRole"/> metadata.
    /// If a role already exists, patches its flags to match the seed definition.
    /// </summary>
    private static async Task SeedRolesAsync(RoleManager<ApplicationRole> roleManager, ILogger logger)
    {
        foreach (var seed in SeedRoles)
        {
            var existing = await roleManager.FindByNameAsync(seed.Name);
            if (existing is not null)
            {
                // Patch existing role with seed flags
                existing.DisplayName = seed.DisplayName;
                existing.Description = seed.Description;
                existing.IsSystem = seed.IsSystem;
                existing.RequiresMinimumUser = seed.RequiresMinimumUser;
                existing.IsDefault = seed.IsDefault;
                existing.Position = seed.Position;

                var updateResult = await roleManager.UpdateAsync(existing);
                if (updateResult.Succeeded)
                    logger.LogInformation("Patched existing role '{Role}' with seed flags.", seed.Name);
                else
                    logger.LogWarning("Failed to patch role '{Role}': {Errors}",
                        seed.Name, FormatErrors(updateResult));

                continue;
            }

            var role = new ApplicationRole
            {
                Name = seed.Name,
                DisplayName = seed.DisplayName,
                Description = seed.Description,
                IsActive = true,
                CreatedUtc = DateTime.UtcNow,
                IsSystem = seed.IsSystem,
                RequiresMinimumUser = seed.RequiresMinimumUser,
                IsDefault = seed.IsDefault,
                Position = seed.Position,
            };

            var result = await roleManager.CreateAsync(role);

            if (result.Succeeded)
                logger.LogInformation("Seeded role '{Role}'.", seed.Name);
            else
                logger.LogWarning("Failed to seed role '{Role}': {Errors}",
                    seed.Name, FormatErrors(result));
        }
    }

    /// <summary>
    /// Creates any users in <see cref="SeedUsers"/> that do not yet exist,
    /// and assigns each to their designated role.
    /// </summary>
    private static async Task SeedUsersAsync(UserManager<ApplicationUser> userManager, ILogger logger)
    {
        foreach (var seed in SeedUsers)
        {
            var existing = await userManager.FindByEmailAsync(seed.Email);
            if (existing is not null) continue;

            var user = new ApplicationUser
            {
                UserName = seed.UserName,
                Email = seed.Email,
                EmailConfirmed = true,          // skip email confirmation for seed accounts
                FirstName = seed.FirstName,
                LastName = seed.LastName,
                DisplayName = seed.DisplayName,
                IsActive = true,
                CreatedUtc = DateTime.UtcNow,
            };

            var createResult = await userManager.CreateAsync(user, seed.Password);

            if (!createResult.Succeeded)
            {
                logger.LogWarning("Failed to seed user '{Email}': {Errors}",
                    seed.Email, FormatErrors(createResult));
                continue;
            }

            var roleResult = await userManager.AddToRoleAsync(user, seed.Role);

            if (roleResult.Succeeded)
                logger.LogInformation("Seeded user '{Email}' with role '{Role}'.",
                    seed.Email, seed.Role);
            else
                logger.LogWarning("Seeded user '{Email}' but failed to assign role '{Role}': {Errors}",
                    seed.Email, seed.Role, FormatErrors(roleResult));
        }
    }

    /// <summary>
    /// Seeds default <see cref="PagePermission"/> records based on the navigation structure
    /// defined in <see cref="INavigationProvider"/>. This ensures the permission-based
    /// authorization system has sensible defaults matching the pre-migration access model.
    /// </summary>
    /// <remarks>
    /// <para><b>Seed Strategy:</b></para>
    /// <list type="bullet">
    ///   <item>Admin role receives grants for ALL navigable pages (admin + non-admin),
    ///         preserving the pre-migration behavior where Admin had full access.</item>
    ///   <item>Other system roles receive grants for non-admin pages only (those not under
    ///         the "admin/" path prefix), preserving access to general application features.</item>
    ///   <item>Custom (non-system) roles are NOT auto-synced — their permissions are managed
    ///         exclusively through the admin UI.</item>
    ///   <item>System_Pages (Login, Register, etc.) are excluded because they always bypass
    ///         permission checks at both handler and context level.</item>
    /// </list>
    /// <para><b>Idempotency:</b> Uses existence checks before insert to prevent duplicates.
    /// The unique composite index on (RoleId, PagePath) provides a safety net.</para>
    /// <para><b>Sync on every run:</b> After the initial full seed, subsequent runs still check
    /// for any new pages added to the navigation provider and grant them to system roles.
    /// This ensures new features are accessible without requiring manual admin configuration.</para>
    /// </remarks>
    private static async Task SeedPagePermissionsAsync(
        ApplicationDbContext dbContext,
        RoleManager<ApplicationRole> roleManager,
        INavigationProvider navigationProvider,
        ILogger logger)
    {
        // Extract all Link NavItems from the navigation provider, including those nested in groups.
        // These represent the universe of configurable pages in the system.
        var allPages = navigationProvider.GetAllLinkPages();

        if (allPages.Count == 0)
        {
            logger.LogWarning("No navigable pages found in DefaultNavigationProvider. Skipping page permission seeding.");
            return;
        }

        // Only sync permissions for system roles (IsSystem=true). Custom roles created
        // through the admin UI have their permissions managed manually — the seed should
        // not auto-grant pages to them and potentially override admin-configured access.
        var systemRoles = await roleManager.Roles
            .Where(r => r.IsSystem)
            .ToListAsync();

        if (systemRoles.Count == 0)
        {
            logger.LogWarning("No system roles found. Skipping page permission sync.");
            return;
        }

        // Get existing permissions to determine what's already granted.
        var existingPermissions = await dbContext.PagePermissions
            .Select(p => new { p.RoleId, p.PagePath })
            .ToListAsync();

        var existingPermissionSet = new HashSet<(string RoleId, string PagePath)>(
            existingPermissions.Select(p => (p.RoleId, p.PagePath)),
            new RolePagePathComparer());

        // Separate pages into admin pages (under "admin/" path) and non-admin pages.
        // Admin pages are gated by the page permission system (database-driven whitelist);
        // non-admin pages are accessible to any authenticated user.
        var nonAdminPages = allPages.Where(p => !p.PagePath.StartsWith("/admin/", StringComparison.OrdinalIgnoreCase)).ToList();

        var addedCount = 0;

        foreach (var role in systemRoles)
        {
            // Determine which pages this system role should receive:
            // - Admin role gets ALL pages (full access, matching pre-migration behavior)
            // - Other system roles get only non-admin pages (general features)
            var pagesToGrant = string.Equals(role.Name, "Admin", StringComparison.OrdinalIgnoreCase)
                ? allPages
                : nonAdminPages;

            foreach (var page in pagesToGrant)
            {
                // Only add permissions that don't already exist (idempotent).
                if (!existingPermissionSet.Contains((role.Id, page.PagePath)))
                {
                    dbContext.PagePermissions.Add(new PagePermission
                    {
                        RoleId = role.Id,
                        PagePath = page.PagePath,
                        PageDisplayName = page.DisplayName
                    });
                    addedCount++;
                }
            }
        }

        if (addedCount > 0)
        {
            await dbContext.SaveChangesAsync();
            logger.LogInformation(
                "Synced page permissions: added {Count} new grant(s) for {RoleCount} system role(s).",
                addedCount, systemRoles.Count);
        }
        else
        {
            logger.LogInformation("All page permissions for system roles are up to date. No new grants needed.");
        }
    }

    /// <summary>
    /// Custom equality comparer for (RoleId, PagePath) tuples using case-insensitive
    /// comparison on PagePath to match the permission lookup behavior.
    /// </summary>
    private sealed class RolePagePathComparer : IEqualityComparer<(string RoleId, string PagePath)>
    {
        public bool Equals((string RoleId, string PagePath) x, (string RoleId, string PagePath) y)
            => string.Equals(x.RoleId, y.RoleId, StringComparison.Ordinal)
            && string.Equals(x.PagePath, y.PagePath, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string RoleId, string PagePath) obj)
            => HashCode.Combine(
                obj.RoleId.GetHashCode(StringComparison.Ordinal),
                obj.PagePath.GetHashCode(StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Formats <see cref="IdentityResult"/> errors into a single readable string for logging.
    /// </summary>
    private static string FormatErrors(IdentityResult result)
        => string.Join(", ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));

    /// <summary>
    /// Internal record representing a role to be seeded on startup.
    /// </summary>
    private sealed record SeedRole(
        string Name,
        string DisplayName,
        string Description,
        bool IsSystem = false,
        bool RequiresMinimumUser = false,
        bool IsDefault = false,
        int Position = 0
    );

    /// <summary>
    /// Internal record representing a user to be seeded on startup.
    /// </summary>
    private sealed record SeedUser(
        string Email,
        string UserName,
        string FirstName,
        string LastName,
        string DisplayName,
        string Password,
        string Role
    );

    /// <summary>
    /// Marker class used solely for typed <see cref="ILogger"/> resolution
    /// since static classes cannot be used as generic type parameters.
    /// </summary>
    private sealed class SeedMarker;

    /// <summary>
    /// Seeds sample announcements for development and demonstration purposes.
    /// Skips seeding if announcements already exist in the database.
    /// </summary>
    private static async Task SeedAnnouncementsAsync(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ILogger logger)
    {
        // Skip if announcements already exist
        if (await dbContext.Announcements.AnyAsync())
        {
            logger.LogInformation("Announcements already seeded. Skipping.");
            return;
        }

        // Use the admin user as the creator
        var admin = await userManager.FindByEmailAsync("admin@example.com");
        if (admin is null)
        {
            logger.LogWarning("Admin user not found. Skipping announcement seeding.");
            return;
        }

        var utcNow = DateTime.UtcNow;

        var announcements = new List<Announcement>
        {
            // Active Banner — Critical (system maintenance)
            new()
            {
                Id = Guid.NewGuid(),
                Title = "Scheduled System Maintenance",
                Content = "<p>The system will undergo scheduled maintenance on <strong>Saturday, July 12th from 10:00 PM to 2:00 AM UTC</strong>.</p><p>During this window:</p><ul><li>The application will be temporarily unavailable</li><li>All active sessions will be terminated</li><li>Pending operations will be queued and processed after maintenance</li></ul><p>Please save your work before the maintenance window begins. We apologize for any inconvenience.</p>",
                DisplayType = AnnouncementDisplayType.Banner,
                Severity = AnnouncementSeverity.Critical,
                StartsAtUtc = null,
                ExpiresAtUtc = utcNow.AddDays(7),
                IsActive = true,
                NotifyUsers = false,
                CreatedByUserId = admin.Id,
                CreatedAtUtc = utcNow.AddHours(-2),
                UpdatedAtUtc = utcNow.AddHours(-2)
            },

            // Active Banner — Warning (security policy)
            new()
            {
                Id = Guid.NewGuid(),
                Title = "Password Policy Update",
                Content = "<p>Effective immediately, the following password requirements have been updated:</p><ul><li>Minimum length increased from 8 to 12 characters</li><li>Must include at least one uppercase letter, one number, and one special character</li><li>Passwords cannot match any of your last 5 previous passwords</li></ul><p>Existing passwords remain valid until your next scheduled rotation. Please update your password at your earliest convenience via <strong>Account &gt; Settings &gt; Security</strong>.</p>",
                DisplayType = AnnouncementDisplayType.Banner,
                Severity = AnnouncementSeverity.Warning,
                StartsAtUtc = null,
                ExpiresAtUtc = utcNow.AddDays(14),
                IsActive = true,
                NotifyUsers = false,
                CreatedByUserId = admin.Id,
                CreatedAtUtc = utcNow.AddDays(-1),
                UpdatedAtUtc = utcNow.AddDays(-1)
            },

            // Active Standard — Info (new feature)
            new()
            {
                Id = Guid.NewGuid(),
                Title = "New Feature: Excel Export for Audit Logs",
                Content = "<p>We're excited to announce a new feature in the administration module:</p><p><strong>Audit Log Excel Export</strong> is now available! Administrators can now export filtered audit log entries to Excel format for compliance reporting and offline analysis.</p><p>To use this feature:</p><ol><li>Navigate to <strong>Admin &gt; Audit Log</strong></li><li>Apply your desired filters (date range, user, action type)</li><li>Click the <strong>Export</strong> button in the top-right corner</li></ol><p>The export includes all visible columns and respects your current filter selections.</p>",
                DisplayType = AnnouncementDisplayType.Standard,
                Severity = AnnouncementSeverity.Info,
                StartsAtUtc = null,
                ExpiresAtUtc = null,
                IsActive = true,
                NotifyUsers = true,
                CreatedByUserId = admin.Id,
                CreatedAtUtc = utcNow.AddDays(-3),
                UpdatedAtUtc = utcNow.AddDays(-3)
            },

            // Active Standard — Info (welcome)
            new()
            {
                Id = Guid.NewGuid(),
                Title = "Welcome to the Aspire Web App Template",
                Content = "<p>Welcome to the enterprise web application template built on <strong>.NET Aspire</strong> and <strong>Blazor Server</strong>.</p><p>This template includes:</p><ul><li>User and role management with LDAP integration</li><li>Database-driven page permissions</li><li>Real-time notification system</li><li>Audit logging with change tracking</li><li>Announcement and banner system</li><li>Theme customization (Light, Dark, System)</li></ul><p>Explore the admin module to see all available features. If you have questions, reach out to the platform team.</p>",
                DisplayType = AnnouncementDisplayType.Standard,
                Severity = AnnouncementSeverity.Info,
                StartsAtUtc = null,
                ExpiresAtUtc = null,
                IsActive = true,
                NotifyUsers = false,
                CreatedByUserId = admin.Id,
                CreatedAtUtc = utcNow.AddDays(-7),
                UpdatedAtUtc = utcNow.AddDays(-7)
            },

            // Scheduled — Info (upcoming feature)
            new()
            {
                Id = Guid.NewGuid(),
                Title = "Upcoming: Dark Mode Improvements",
                Content = "<p>We're working on improvements to the dark mode theme based on user feedback:</p><ul><li>Better contrast ratios for accessibility compliance</li><li>Consistent styling across all data grid components</li><li>Reduced eye strain during extended usage sessions</li></ul><p>These improvements will be rolled out in the next release cycle. Stay tuned!</p>",
                DisplayType = AnnouncementDisplayType.Standard,
                Severity = AnnouncementSeverity.Info,
                StartsAtUtc = utcNow.AddDays(5),
                ExpiresAtUtc = utcNow.AddDays(30),
                IsActive = true,
                NotifyUsers = true,
                CreatedByUserId = admin.Id,
                CreatedAtUtc = utcNow.AddDays(-2),
                UpdatedAtUtc = utcNow.AddDays(-2)
            },

            // Expired — Warning (recently expired, shows in list page)
            new()
            {
                Id = Guid.NewGuid(),
                Title = "Database Migration Completed",
                Content = "<p>The planned database migration has been <strong>completed successfully</strong>.</p><p>All data has been verified and the application is operating normally. If you notice any issues or missing data, please report them immediately to the platform team.</p><p>Thank you for your patience during the maintenance window.</p>",
                DisplayType = AnnouncementDisplayType.Banner,
                Severity = AnnouncementSeverity.Warning,
                StartsAtUtc = utcNow.AddDays(-10),
                ExpiresAtUtc = utcNow.AddDays(-3),
                IsActive = true,
                NotifyUsers = false,
                CreatedByUserId = admin.Id,
                CreatedAtUtc = utcNow.AddDays(-10),
                UpdatedAtUtc = utcNow.AddDays(-10)
            },

            // Draft — not yet published
            new()
            {
                Id = Guid.NewGuid(),
                Title = "Q3 Platform Roadmap",
                Content = "<p>Draft: Quarterly roadmap update for stakeholder review.</p><p><em>This announcement is not yet published.</em></p>",
                DisplayType = AnnouncementDisplayType.Standard,
                Severity = AnnouncementSeverity.Info,
                StartsAtUtc = null,
                ExpiresAtUtc = null,
                IsActive = false,
                NotifyUsers = true,
                CreatedByUserId = admin.Id,
                CreatedAtUtc = utcNow.AddHours(-6),
                UpdatedAtUtc = utcNow.AddHours(-6)
            },
        };

        dbContext.Announcements.AddRange(announcements);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Seeded {Count} sample announcements.", announcements.Count);
    }
}