// Feature: notification-system, Property 12: Missing preferences default to both channels enabled
using AspireWebAppTemplate.ApiService.Data;
using AspireWebAppTemplate.ApiService.Data.Entities;
using AspireWebAppTemplate.ApiService.Services;
using AspireWebAppTemplate.Core.Domain.Enums;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AspireWebAppTemplate.Tests.Notifications;

/// <summary>
/// Property-based tests verifying that missing notification preferences default
/// to both channels (InAppEnabled and EmailEnabled) set to true for all categories.
/// </summary>
/// <remarks>
/// **Validates: Requirements 9.4**
/// </remarks>
public class NotificationPreferenceDefaultsPropertyTests
{
    /// <summary>
    /// Creates a SQLite in-memory ApplicationDbContext for testing.
    /// Foreign key enforcement is disabled to allow testing preferences
    /// without satisfying all ApplicationUser relational constraints.
    /// </summary>
    private static (ApplicationDbContext dbContext, SqliteConnection connection) CreateDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        // Disable FK enforcement so we can test preference retrieval
        // without needing to satisfy all ApplicationUser relational constraints.
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = OFF;";
        cmd.ExecuteNonQuery();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new ApplicationDbContext(options);
        dbContext.Database.EnsureCreated();
        return (dbContext, connection);
    }

    /// <summary>
    /// Property: For any user with no NotificationPreference records in the database,
    /// retrieving preferences SHALL return one entry per NotificationCategory enum value
    /// with InAppEnabled=true and EmailEnabled=true for every category.
    /// **Validates: Requirements 9.4**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property GetPreferences_WhenNoRecordsExist_DefaultsToBothChannelsEnabled()
    {
        // Generator for random user ID strings.
        var userIdGen = Gen.Elements("user-alpha", "user-beta", "user-gamma", "user-delta")
            .Select(prefix => $"{prefix}-{Guid.NewGuid():N}");

        return Prop.ForAll(
            Arb.From(userIdGen),
            (string userId) =>
            {
                var (dbContext, connection) = CreateDbContext();
                try
                {
                    // No preference records are seeded for this user — the user has no stored preferences.
                    var logger = NullLogger<NotificationService>.Instance;
                    var service = new NotificationService(dbContext, logger);

                    // Act: retrieve preferences for a user with no preference records.
                    var preferences = service.GetPreferencesAsync(userId).GetAwaiter().GetResult();

                    // Verify: one entry per NotificationCategory enum value.
                    var allCategories = Enum.GetValues<NotificationCategory>();
                    var correctCount = preferences.Count == allCategories.Length;

                    // Verify: every entry has InAppEnabled=true and EmailEnabled=true.
                    var allInAppEnabled = preferences.All(p => p.InAppEnabled);
                    var allEmailEnabled = preferences.All(p => p.EmailEnabled);

                    // Verify: all categories are represented.
                    var returnedCategories = preferences.Select(p => p.Category).OrderBy(c => c).ToList();
                    var expectedCategories = allCategories.OrderBy(c => c).ToList();
                    var allCategoriesPresent = returnedCategories.SequenceEqual(expectedCategories);

                    var allMatch = correctCount && allInAppEnabled && allEmailEnabled && allCategoriesPresent;

                    return allMatch.Label(
                        $"Preference defaults failed for userId='{userId}'. " +
                        $"CorrectCount={correctCount} (got {preferences.Count}, expected {allCategories.Length}), " +
                        $"AllInAppEnabled={allInAppEnabled}, AllEmailEnabled={allEmailEnabled}, " +
                        $"AllCategoriesPresent={allCategoriesPresent}");
                }
                finally
                {
                    dbContext.Dispose();
                    connection.Dispose();
                }
            });
    }
}
