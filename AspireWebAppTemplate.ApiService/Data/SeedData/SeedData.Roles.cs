using AspireWebAppTemplate.ApiService.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace AspireWebAppTemplate.ApiService.Data;

public static partial class SeedData
{
    #region Roles

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

    #endregion
}
