// Feature: notification-system, Property 9: Bulk dismiss deletes only owned-and-existing notifications
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Data.Entities;
using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Infrastructure.Services;
using AspireWebAppTemplate.Domain.Enums;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AspireWebAppTemplate.Tests.Notifications;

/// <summary>
/// Property-based tests verifying bulk dismiss behavior:
/// - Only notifications that exist AND belong to the specified user are deleted.
/// - Notifications belonging to other users are never affected.
/// - Non-existent IDs are silently ignored.
/// - The returned count equals the number of owned notifications actually deleted.
/// </summary>
/// <remarks>
/// **Validates: Requirements 5.1, 5.2**
/// </remarks>
public class BulkDismissPropertyTests
{
    /// <summary>
    /// Creates a SQLite in-memory ApplicationDbContext for testing.
    /// Foreign key enforcement is disabled to allow seeding notifications
    /// without satisfying all ApplicationUser relational constraints.
    /// </summary>
    private static (ApplicationDbContext dbContext, SqliteConnection connection) CreateDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        // Disable FK enforcement so we can test bulk dismiss logic
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
    /// Seeds a notification entity for the specified user with a random category.
    /// Returns the notification's ID.
    /// </summary>
    private static Guid SeedNotification(ApplicationDbContext dbContext, string userId, NotificationCategory category)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Category = category,
            Title = $"Notification for {userId[..8]}",
            Message = $"Message body for category {category}.",
            IsRead = false,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.Notifications.Add(notification);
        return notification.Id;
    }

    /// <summary>
    /// Property: For any list of notification IDs containing a mix of IDs belonging to
    /// user A, IDs belonging to user B, and non-existent IDs, bulk dismiss called with
    /// user A SHALL delete exactly those notifications that exist AND belong to user A,
    /// leaving all user B notifications untouched, and returning the count of user A
    /// notifications actually deleted.
    /// **Validates: Requirements 5.1, 5.2**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property BulkDismiss_DeletesOnlyOwnedAndExistingNotifications()
    {
        // Generator for NotificationCategory enum values.
        var categoryGen = Gen.Elements(Enum.GetValues<NotificationCategory>());

        return Prop.ForAll(
            Arb.From(categoryGen),
            (NotificationCategory category) =>
            {
                var (dbContext, connection) = CreateDbContext();
                try
                {
                    // Define two users.
                    var userA = "user-a-" + Guid.NewGuid().ToString()[..8];
                    var userB = "user-b-" + Guid.NewGuid().ToString()[..8];

                    // Seed notifications for user A (the acting user).
                    var userAId1 = SeedNotification(dbContext, userA, category);
                    var userAId2 = SeedNotification(dbContext, userA, category);
                    var userAId3 = SeedNotification(dbContext, userA, category);

                    // Seed notifications for user B (another user).
                    var userBId1 = SeedNotification(dbContext, userB, category);
                    var userBId2 = SeedNotification(dbContext, userB, category);

                    dbContext.SaveChanges();

                    // Build the mixed ID list to dismiss:
                    // - Some IDs belonging to user A (should be deleted)
                    // - Some IDs belonging to user B (should NOT be deleted)
                    // - Some non-existent IDs (should be silently ignored)
                    var nonExistentId1 = Guid.NewGuid();
                    var nonExistentId2 = Guid.NewGuid();

                    var idsToDismiss = new List<Guid>
                    {
                        userAId1,       // owned by A — should be deleted
                        userAId2,       // owned by A — should be deleted
                        userBId1,       // owned by B — should NOT be deleted
                        userBId2,       // owned by B — should NOT be deleted
                        nonExistentId1, // doesn't exist — silently ignored
                        nonExistentId2  // doesn't exist — silently ignored
                    };

                    // Create the service and call BulkDismissAsync.
                    var service = new NotificationService(dbContext, NullLogger<NotificationService>.Instance, null!);
                    var deletedCount = service.BulkDismissAsync(userA, idsToDismiss).GetAwaiter().GetResult();

                    // Clear the change tracker so retrieval hits the database.
                    dbContext.ChangeTracker.Clear();

                    // Verify: returned count equals only the user A IDs that existed in the dismiss list.
                    var expectedDeletedCount = 2; // userAId1 and userAId2
                    var countCorrect = deletedCount == expectedDeletedCount;

                    // Verify: user A notifications in the ID list are deleted.
                    var userAId1Deleted = !dbContext.Notifications.Any(n => n.Id == userAId1);
                    var userAId2Deleted = !dbContext.Notifications.Any(n => n.Id == userAId2);

                    // Verify: user A notification NOT in the dismiss list is still present.
                    var userAId3StillExists = dbContext.Notifications.Any(n => n.Id == userAId3);

                    // Verify: all user B notifications are still present.
                    var userBId1StillExists = dbContext.Notifications.Any(n => n.Id == userBId1);
                    var userBId2StillExists = dbContext.Notifications.Any(n => n.Id == userBId2);

                    var allCorrect = countCorrect && userAId1Deleted && userAId2Deleted &&
                                     userAId3StillExists && userBId1StillExists && userBId2StillExists;

                    return allCorrect.Label(
                        $"BulkDismiss failed. DeletedCount={deletedCount} (expected {expectedDeletedCount}), " +
                        $"UserA_Id1_Deleted={userAId1Deleted}, UserA_Id2_Deleted={userAId2Deleted}, " +
                        $"UserA_Id3_StillExists={userAId3StillExists}, " +
                        $"UserB_Id1_StillExists={userBId1StillExists}, UserB_Id2_StillExists={userBId2StillExists}");
                }
                finally
                {
                    dbContext.Dispose();
                    connection.Dispose();
                }
            });
    }
}
