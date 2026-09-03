using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Application.Features.Template.Navigation;
using AspireWebAppTemplate.Application.Extensions;
using AspireWebAppTemplate.Infrastructure.Data.Entities.Template;
using AspireWebAppTemplate.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AspireWebAppTemplate.Infrastructure.Data.SeedData;

public static partial class SeedData
{
    #region Page Permissions

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

    #endregion
}
