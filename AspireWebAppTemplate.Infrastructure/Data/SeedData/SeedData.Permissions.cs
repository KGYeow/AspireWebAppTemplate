using AspireWebAppTemplate.Infrastructure.Data.Entities;
using AspireWebAppTemplate.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AspireWebAppTemplate.Infrastructure.Data.SeedData;

public static partial class SeedData
{
    #region Permissions

    /// <summary>
    /// The canonical set of 16 permission definitions for the resource-based authorization model,
    /// expressed as (Key, DisplayName, Module) tuples in <c>Module.Action</c> key format.
    /// </summary>
    /// <remarks>
    /// These are the complete, authoritative permission definitions. The seed process inserts any
    /// of these whose <see cref="Permission.Key"/> does not already exist and leaves existing rows
    /// unchanged, so new keys added here are picked up on subsequent runs.
    /// </remarks>
    private static readonly (string Key, string DisplayName, string Module)[] SeedPermissions =
    [
        ("Users.Read",            "View Users",                 "Users"),
        ("Users.Create",          "Create Users",               "Users"),
        ("Users.Update",          "Update Users",               "Users"),
        ("Users.Delete",          "Delete Users",               "Users"),
        ("Users.Activate",        "Activate/Deactivate Users",  "Users"),
        ("Roles.Read",            "View Roles",                 "Roles"),
        ("Roles.Manage",          "Manage Roles",               "Roles"),
        ("AuditLog.Read",         "View Audit Log",             "AuditLog"),
        ("AuditLog.Export",       "Export Audit Log",           "AuditLog"),
        ("Permissions.Manage",    "Manage Permissions",         "Permissions"),
        ("Announcements.Read",    "View Announcements",         "Announcements"),
        ("Announcements.Create",  "Create Announcements",       "Announcements"),
        ("Announcements.Update",  "Update Announcements",       "Announcements"),
        ("Announcements.Delete",  "Delete Announcements",       "Announcements"),
        ("EmailTemplates.Read",   "View Email Templates",       "EmailTemplates"),
        ("EmailTemplates.Update", "Edit Email Templates",       "EmailTemplates"),
    ];

    /// <summary>
    /// Seeds the resource-based authorization permission definitions and grants all of them to the
    /// Admin role. Both the permission definitions and the Admin grants are inserted idempotently.
    /// </summary>
    /// <param name="dbContext">The application database context used to read and write permission records.</param>
    /// <param name="roleManager">The role manager used to resolve the Admin role.</param>
    /// <param name="logger">The logger used to report seeding progress and per-record failures.</param>
    /// <remarks>
    /// <para><b>Seed Strategy:</b></para>
    /// <list type="bullet">
    ///   <item>Permission definitions are upserted by <see cref="Permission.Key"/>: a definition is
    ///         inserted only when its key does not already exist; existing rows are left unchanged.
    ///         On subsequent runs only brand-new keys are added.</item>
    ///   <item>All seeded permissions are granted to the Admin role via <see cref="RolePermission"/>
    ///         records; only missing grants are inserted so the operation is idempotent.</item>
    ///   <item>If the Admin role does not exist the permission definitions are still seeded, but the
    ///         role assignment is skipped with a warning.</item>
    /// </list>
    /// <para><b>Resilience:</b> Each permission insert and each Admin grant insert is saved
    /// individually; a per-record database failure is logged at Error level and seeding continues
    /// with the remaining records.</para>
    /// </remarks>
    private static async Task SeedPermissionsAsync(ApplicationDbContext dbContext, RoleManager<ApplicationRole> roleManager, ILogger logger)
    {
        // Determine which permission keys already exist so only new definitions are inserted
        // (idempotent upsert; existing rows are never modified).
        var existingKeys = new HashSet<string>(
            await dbContext.Permissions.Select(p => p.Key).ToListAsync(),
            StringComparer.Ordinal);

        var insertedCount = 0;

        foreach (var (key, displayName, module) in SeedPermissions)
        {
            if (existingKeys.Contains(key))
                continue;

            try
            {
                dbContext.Permissions.Add(new Permission
                {
                    Key = key,
                    DisplayName = displayName,
                    Module = module
                });

                // Save per-record so a failure on one permission does not abort the rest.
                await dbContext.SaveChangesAsync();
                insertedCount++;
            }
            catch (Exception ex)
            {
                // Detach the failed entry so it does not block subsequent SaveChanges calls,
                // then continue seeding the remaining permissions.
                var entry = dbContext.ChangeTracker.Entries<Permission>()
                    .FirstOrDefault(e => e.State == EntityState.Added && e.Entity.Key == key);
                if (entry is not null)
                    entry.State = EntityState.Detached;

                logger.LogError(ex, "Failed to seed permission '{Key}'. Continuing with remaining permissions.", key);
            }
        }

        if (insertedCount > 0)
            logger.LogInformation("Seeded {Count} new permission definition(s).", insertedCount);
        else
            logger.LogInformation("All permission definitions are up to date. No new permissions inserted.");

        await AssignPermissionsToAdminAsync(dbContext, roleManager, logger);
    }

    /// <summary>
    /// Grants every seeded permission to the Admin role, inserting only the grants that do not
    /// already exist. If the Admin role is absent the assignment is skipped with a warning.
    /// </summary>
    /// <param name="dbContext">The application database context used to read and write grant records.</param>
    /// <param name="roleManager">The role manager used to resolve the Admin role.</param>
    /// <param name="logger">The logger used to report assignment progress and per-record failures.</param>
    private static async Task AssignPermissionsToAdminAsync(
        ApplicationDbContext dbContext,
        RoleManager<ApplicationRole> roleManager,
        ILogger logger)
    {
        var adminRole = await roleManager.FindByNameAsync("Admin");
        if (adminRole is null)
        {
            // The Admin role is seeded before permissions, so its absence is unexpected. Still seed
            // the definitions (already done) but skip the grants rather than fail.
            logger.LogWarning("Admin role not found. Skipping permission assignment to Admin.");
            return;
        }

        // Load all permission ids and the Admin role's existing grants to drive an idempotent insert.
        var allPermissionIds = await dbContext.Permissions.Select(p => p.Id).ToListAsync();

        var existingGrantIds = new HashSet<int>(
            await dbContext.RolePermissions
                .Where(rp => rp.RoleId == adminRole.Id)
                .Select(rp => rp.PermissionId)
                .ToListAsync());

        var grantedCount = 0;

        foreach (var permissionId in allPermissionIds)
        {
            if (existingGrantIds.Contains(permissionId))
                continue;

            try
            {
                dbContext.RolePermissions.Add(new RolePermission
                {
                    RoleId = adminRole.Id,
                    PermissionId = permissionId
                });

                // Save per-record so a failure on one grant does not abort the rest.
                await dbContext.SaveChangesAsync();
                grantedCount++;
            }
            catch (Exception ex)
            {
                // Detach the failed entry so it does not block subsequent SaveChanges calls,
                // then continue granting the remaining permissions.
                var entry = dbContext.ChangeTracker.Entries<RolePermission>()
                    .FirstOrDefault(e => e.State == EntityState.Added
                        && e.Entity.RoleId == adminRole.Id
                        && e.Entity.PermissionId == permissionId);
                if (entry is not null)
                    entry.State = EntityState.Detached;

                logger.LogError(ex,
                    "Failed to grant permission {PermissionId} to Admin role. Continuing with remaining grants.",
                    permissionId);
            }
        }

        if (grantedCount > 0)
            logger.LogInformation("Granted {Count} new permission(s) to the Admin role.", grantedCount);
        else
            logger.LogInformation("Admin role already holds all permissions. No new grants needed.");
    }

    #endregion
}
