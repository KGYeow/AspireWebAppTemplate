using AspireWebAppTemplate.ApiService.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace AspireWebAppTemplate.ApiService.Data;

public static partial class SeedData
{
    #region Users

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

    #endregion
}
