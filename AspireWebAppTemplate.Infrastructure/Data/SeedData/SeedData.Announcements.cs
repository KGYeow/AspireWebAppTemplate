using AspireWebAppTemplate.Domain.Enums;
using AspireWebAppTemplate.Infrastructure.Data.Entities.Template;
using AspireWebAppTemplate.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AspireWebAppTemplate.Infrastructure.Data.SeedData;

public static partial class SeedData
{
    #region Announcements

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

    #endregion
}
