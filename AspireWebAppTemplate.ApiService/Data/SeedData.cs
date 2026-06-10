using BlazorWebAppTemplate.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace BlazorWebAppTemplate.Data;

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
    /// Entry point called from <c>Program.cs</c> to seed roles and users.
    /// Skips creation of any role or user that already exists.
    /// </summary>
    /// <param name="services">
    /// A scoped <see cref="IServiceProvider"/> from which Identity services are resolved.
    /// </param>
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = services.GetRequiredService<ILogger<SeedMarker>>();

        await SeedRolesAsync(roleManager, logger);
        await SeedUsersAsync(userManager, logger);
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