// Feature: notification-system, Property 10: Mark-all-as-read updates all unread and returns correct count
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Data.Entities;
using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Infrastructure.Services;
using AspireWebAppTemplate.Infrastructure.Services.Template.Notifications;
using AspireWebAppTemplate.Domain.Enums;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AspireWebAppTemplate.Tests.Notifications;

/// <summary>
/// Property-based tests verifying that MarkAllAsReadAsync correctly updates all unread
/// notifications for a user and returns the accurate count of updated items.
/// </summary>
/// <remarks>
/// **Validates: Requirements 6.1, 6.2**
/// </remarks>
public class MarkAllAsReadPropertyTests
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

        // Disable FK enforcement so we can test notification logic
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
    /// Seeds N unread notifications and M already-read notifications for the given user.
    /// Returns the IDs of the unread notifications and the already-read notifications separately.
    /// </summary>
    private static (List<Guid> unreadIds, List<Guid> alreadyReadIds) SeedNotifications(
        ApplicationDbContext dbContext,
        string userId,
        int unreadCount,
        int alreadyReadCount)
    {
        var unreadIds = new List<Guid>(unreadCount);
        var alreadyReadIds = new List<Guid>(alreadyReadCount);
        var categories = Enum.GetValues<NotificationCategory>();

        // Seed unread notifications
        for (var i = 0; i < unreadCount; i++)
        {
            var id = Guid.NewGuid();
            unreadIds.Add(id);
            dbContext.Notifications.Add(new Notification
            {
                Id = id,
                UserId = userId,
                Category = categories[i % categories.Length],
                Title = $"Unread Notification {i + 1}",
                Message = $"Unread message body {i + 1}",
                IsRead = false,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-unreadCount + i),
                ReadAtUtc = null
            });
        }

        // Seed already-read notifications with a fixed ReadAtUtc timestamp
        var readTimestamp = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < alreadyReadCount; i++)
        {
            var id = Guid.NewGuid();
            alreadyReadIds.Add(id);
            dbContext.Notifications.Add(new Notification
            {
                Id = id,
                UserId = userId,
                Category = categories[i % categories.Length],
                Title = $"Already Read Notification {i + 1}",
                Message = $"Already read message body {i + 1}",
                IsRead = true,
                CreatedAtUtc = DateTime.UtcNow.AddHours(-alreadyReadCount + i),
                ReadAtUtc = readTimestamp
            });
        }

        dbContext.SaveChanges();
        return (unreadIds, alreadyReadIds);
    }

    /// <summary>
    /// Property: For any set of notifications belonging to a user with N unread items,
    /// MarkAllAsReadAsync SHALL set IsRead=true and ReadAtUtc on all N items, and return
    /// exactly N as the updated count. Already-read notifications are not modified.
    /// **Validates: Requirements 6.1, 6.2**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property MarkAllAsRead_UpdatesAllUnread_ReturnsCorrectCount()
    {
        // Generate N (unread count) between 3 and 20.
        var unreadCountGen = Gen.Choose(3, 20);
        // Generate M (already-read count) between 1 and 10.
        var alreadyReadCountGen = Gen.Choose(1, 10);

        return Prop.ForAll(
            Arb.From(unreadCountGen),
            Arb.From(alreadyReadCountGen),
            (int unreadCount, int alreadyReadCount) =>
            {
                var (dbContext, connection) = CreateDbContext();
                try
                {
                    var userId = Guid.NewGuid().ToString();

                    // Seed N unread and M already-read notifications.
                    var (unreadIds, alreadyReadIds) = SeedNotifications(
                        dbContext, userId, unreadCount, alreadyReadCount);

                    // Capture the original ReadAtUtc values for already-read notifications.
                    var originalReadTimestamps = dbContext.Notifications
                        .Where(n => alreadyReadIds.Contains(n.Id))
                        .ToDictionary(n => n.Id, n => n.ReadAtUtc);

                    var service = new NotificationService(dbContext, NullLogger<NotificationService>.Instance, null!);

                    // Act: mark all as read.
                    var returnedCount = service.MarkAllAsReadAsync(userId).GetAwaiter().GetResult();

                    // Clear change tracker so retrieval hits the database.
                    dbContext.ChangeTracker.Clear();

                    // Verify: returned count equals N (number that were unread).
                    var countCorrect = returnedCount == unreadCount;

                    // Verify: all notifications now have IsRead=true.
                    var allNotifications = dbContext.Notifications
                        .Where(n => n.UserId == userId)
                        .ToList();
                    var allAreRead = allNotifications.All(n => n.IsRead);

                    // Verify: all previously-unread notifications now have ReadAtUtc set (non-null).
                    var previouslyUnread = allNotifications
                        .Where(n => unreadIds.Contains(n.Id))
                        .ToList();
                    var allUnreadHaveReadAt = previouslyUnread.All(n => n.ReadAtUtc.HasValue);

                    // Verify: already-read notifications were not modified (ReadAtUtc unchanged).
                    var previouslyRead = allNotifications
                        .Where(n => alreadyReadIds.Contains(n.Id))
                        .ToList();
                    var alreadyReadUnchanged = previouslyRead.All(n =>
                        n.ReadAtUtc == originalReadTimestamps[n.Id]);

                    var allMatch = countCorrect && allAreRead && allUnreadHaveReadAt && alreadyReadUnchanged;

                    return allMatch.Label(
                        $"MarkAllAsRead failed. ReturnedCount={returnedCount} (expected {unreadCount}), " +
                        $"CountCorrect={countCorrect}, AllAreRead={allAreRead}, " +
                        $"AllUnreadHaveReadAt={allUnreadHaveReadAt}, AlreadyReadUnchanged={alreadyReadUnchanged}");
                }
                finally
                {
                    dbContext.Dispose();
                    connection.Dispose();
                }
            });
    }
}
