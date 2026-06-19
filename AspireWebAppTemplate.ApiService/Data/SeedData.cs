using AspireWebAppTemplate.ApiService.Data.Entities;
using AspireWebAppTemplate.Core.Application.Abstractions;
using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Core.Common.Defaults;
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
    ///   <item>Non-admin roles receive grants for non-admin pages only (those not under
    ///         the "admin/" path prefix), preserving access to general application features.</item>
    ///   <item>System_Pages (Login, Register, etc.) are excluded because they always bypass
    ///         permission checks at both handler and context level.</item>
    /// </list>
    /// <para><b>Idempotency:</b> Uses existence checks before insert to prevent duplicates.
    /// The unique composite index on (RoleId, PagePath) provides a safety net.</para>
    /// </remarks>
    private static async Task SeedPagePermissionsAsync(
        ApplicationDbContext dbContext,
        RoleManager<ApplicationRole> roleManager,
        INavigationProvider navigationProvider,
        ILogger logger)
    {
        // Only seed permissions if the table is completely empty (first-time setup).
        // This prevents overwriting admin-configured permissions on every app restart.
        // After initial seed, all permission management is done through the admin UI.
        var hasAnyPermissions = await dbContext.PagePermissions.AnyAsync();
        if (hasAnyPermissions)
        {
            logger.LogInformation("PagePermissions table already has data. Skipping seed.");
            return;
        }

        // Extract all Link NavItems from the navigation provider, including those nested in groups.
        // These represent the universe of configurable pages in the system.
        var allPages = navigationProvider.GetAllLinkPages();

        if (allPages.Count == 0)
        {
            logger.LogWarning("No navigable pages found in DefaultNavigationProvider. Skipping page permission seeding.");
            return;
        }

        // Retrieve all roles from the database to seed permissions for each.
        var allRoles = await roleManager.Roles.ToListAsync();

        // Separate pages into admin pages (under "admin/" path) and non-admin pages.
        // Admin pages were previously restricted via [Authorize(Roles = "Admin")] attributes;
        // non-admin pages were accessible to any authenticated user.
        var adminPages = allPages.Where(p => p.PagePath.StartsWith("/admin/", StringComparison.OrdinalIgnoreCase)).ToList();
        var nonAdminPages = allPages.Where(p => !p.PagePath.StartsWith("/admin/", StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var role in allRoles)
        {
            // Determine which pages this role should receive:
            // - Admin role gets ALL pages (full access, matching pre-migration behavior)
            // - Non-admin roles get only non-admin pages (general features)
            var pagesToGrant = string.Equals(role.Name, "Admin", StringComparison.OrdinalIgnoreCase)
                ? allPages
                : nonAdminPages;

            foreach (var page in pagesToGrant)
            {
                dbContext.PagePermissions.Add(new PagePermission
                {
                    RoleId = role.Id,
                    PagePath = page.PagePath,
                    PageDisplayName = page.DisplayName
                });
            }
        }

        await dbContext.SaveChangesAsync();
        logger.LogInformation("Seeded initial PagePermission records for {RoleCount} role(s).", allRoles.Count);
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
}