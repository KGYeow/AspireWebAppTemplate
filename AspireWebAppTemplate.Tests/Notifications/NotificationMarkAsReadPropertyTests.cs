// Feature: notification-system, Property 8: Mark-as-read is idempotent
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Data.Entities.Template;
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
/// Property-based tests verifying mark-as-read behavior:
/// - Marking an already-read notification again does not modify the ReadAtUtc timestamp (Property 8)
/// </summary>
/// <remarks>
/// **Validates: Requirements 4.3**
/// </remarks>
public class NotificationMarkAsReadPropertyTests
{
    /// <summary>
    /// Creates a SQLite in-memory ApplicationDbContext for testing.
    /// Foreign key enforcement is disabled to allow seeding notifications
    /// without satisfying all relational constraints.
    /// </summary>
    private static (ApplicationDbContext dbContext, SqliteConnection connection) CreateDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        // Disable FK enforcement so we can test mark-as-read behavior
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
    /// Seeds an unread notification for the specified user and returns the notification ID.
    /// </summary>
    private static (string userId, Guid notificationId) SeedUnreadNotification(
        ApplicationDbContext dbContext,
        NotificationCategory category)
    {
        var userId = Guid.NewGuid().ToString();

        // Seed a minimal ApplicationUser record.
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = $"testuser-{userId[..8]}",
            NormalizedUserName = $"TESTUSER-{userId[..8]}",
            Email = $"test-{userId[..8]}@example.com",
            NormalizedEmail = $"TEST-{userId[..8]}@EXAMPLE.COM",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        dbContext.Users.Add(user);

        // Seed an unread notification.
        var notificationId = Guid.NewGuid();
        var notification = new Notification
        {
            Id = notificationId,
            UserId = userId,
            Category = category,
            Title = "Test Notification",
            Message = "A notification to test idempotent mark-as-read.",
            IsRead = false,
            CreatedAtUtc = DateTime.UtcNow,
            ReadAtUtc = null
        };
        dbContext.Notifications.Add(notification);
        dbContext.SaveChanges();

        return (userId, notificationId);
    }

    /// <summary>
    /// Property: For any notification that is already marked as read (IsRead=true, ReadAtUtc=T),
    /// marking it as read again SHALL not modify the ReadAtUtc value — it SHALL remain equal to T.
    /// Both calls return true, confirming success and idempotency.
    /// **Validates: Requirements 4.3**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property MarkAsRead_WhenCalledTwice_DoesNotModifyReadAtUtc()
    {
        // Generator for random NotificationCategory values.
        var categoryGen = Gen.Elements(Enum.GetValues<NotificationCategory>());

        return Prop.ForAll(
            Arb.From(categoryGen),
            (NotificationCategory category) =>
            {
                var (dbContext, connection) = CreateDbContext();
                try
                {
                    // Seed an unread notification.
                    var (userId, notificationId) = SeedUnreadNotification(dbContext, category);

                    var service = new NotificationService(dbContext, NullLogger<NotificationService>.Instance, null!);

                    // First call: mark as read — this should set IsRead=true and ReadAtUtc.
                    var firstResult = service.MarkAsReadAsync(userId, notificationId).GetAwaiter().GetResult();

                    // Capture ReadAtUtc after the first mark-as-read call.
                    dbContext.ChangeTracker.Clear();
                    var afterFirst = dbContext.Notifications.Single(n => n.Id == notificationId);
                    var firstReadAtUtc = afterFirst.ReadAtUtc;

                    // Add a small delay to ensure system clock would produce a different timestamp
                    // if the service incorrectly overwrites ReadAtUtc on a second call.
                    Thread.Sleep(50);

                    // Second call: mark as read again — should be idempotent.
                    var secondResult = service.MarkAsReadAsync(userId, notificationId).GetAwaiter().GetResult();

                    // Capture ReadAtUtc after the second mark-as-read call.
                    dbContext.ChangeTracker.Clear();
                    var afterSecond = dbContext.Notifications.Single(n => n.Id == notificationId);
                    var secondReadAtUtc = afterSecond.ReadAtUtc;

                    // Both calls should return true.
                    var firstReturnedTrue = firstResult == true;
                    var secondReturnedTrue = secondResult == true;

                    // ReadAtUtc should be unchanged between first and second call.
                    var readAtUtcUnchanged = firstReadAtUtc == secondReadAtUtc;

                    // ReadAtUtc should not be null after first call.
                    var readAtUtcNotNull = firstReadAtUtc is not null;

                    var allPass = firstReturnedTrue && secondReturnedTrue &&
                                  readAtUtcNotNull && readAtUtcUnchanged;

                    return allPass.Label(
                        $"Idempotent mark-as-read failed. " +
                        $"FirstReturned={firstResult}, SecondReturned={secondResult}, " +
                        $"FirstReadAtUtc={firstReadAtUtc:O}, SecondReadAtUtc={secondReadAtUtc:O}, " +
                        $"ReadAtUtcUnchanged={readAtUtcUnchanged}, Category={category}");
                }
                finally
                {
                    dbContext.Dispose();
                    connection.Dispose();
                }
            });
    }
}
