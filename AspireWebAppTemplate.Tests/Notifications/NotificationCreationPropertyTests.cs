// Feature: notification-system, Property 1: Notification creation preserves all input fields
// Feature: notification-system, Property 11: Notification creation respects InAppEnabled preference
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Data.Entities;
using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Infrastructure.Services;
using AspireWebAppTemplate.Application.Contracts.Notifications;
using AspireWebAppTemplate.Domain.Enums;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AspireWebAppTemplate.Tests.Notifications;

/// <summary>
/// Property-based tests verifying notification creation behavior:
/// - All input fields are preserved correctly on the persisted entity (Property 1)
/// - InAppEnabled preferences are respected during creation (Property 11)
/// </summary>
/// <remarks>
/// **Validates: Requirements 1.1, 9.5**
/// </remarks>
public class NotificationCreationPropertyTests
{
    /// <summary>
    /// Creates a SQLite in-memory ApplicationDbContext for testing.
    /// Foreign key enforcement is disabled to allow seeding a minimal ApplicationUser
    /// without satisfying all relational constraints.
    /// </summary>
    private static (ApplicationDbContext dbContext, SqliteConnection connection) CreateDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        // Disable FK enforcement so we can test notification creation
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
    /// Seeds a valid ApplicationUser into the database and returns the user's ID.
    /// </summary>
    private static string SeedUser(ApplicationDbContext dbContext)
    {
        var userId = Guid.NewGuid().ToString();
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
        dbContext.SaveChanges();
        return userId;
    }

    /// <summary>
    /// Property: For any valid CreateNotificationRequest with a known-existing user and
    /// InAppEnabled=true (default), the resulting Notification entity SHALL have matching
    /// UserId, Category, Title, Message, IsRead=false, and a CreatedAtUtc timestamp set
    /// to a UTC value.
    /// **Validates: Requirements 1.1**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property CreatedNotification_PreservesAllInputFields()
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
                    // Seed a valid user so the service's user-exists check passes.
                    var userId = SeedUser(dbContext);

                    var logger = NullLogger<NotificationService>.Instance;
                    var service = new NotificationService(dbContext, logger, null!);

                    var request = new CreateNotificationRequest
                    {
                        UserId = userId,
                        Category = category,
                        Title = $"Test Title for {category}",
                        Message = $"Test message body for category {category} notification."
                    };

                    // Act: create the notification through the service.
                    service.CreateNotificationAsync(request).GetAwaiter().GetResult();

                    // Clear the change tracker so retrieval hits the database.
                    dbContext.ChangeTracker.Clear();

                    // Retrieve the persisted notification.
                    var notification = dbContext.Notifications
                        .SingleOrDefault(n => n.UserId == userId);

                    // Assert all fields are preserved correctly.
                    var exists = notification is not null;
                    var userIdMatch = notification?.UserId == request.UserId;
                    var categoryMatch = notification?.Category == request.Category;
                    var titleMatch = notification?.Title == request.Title;
                    var messageMatch = notification?.Message == request.Message;
                    var isReadFalse = notification?.IsRead == false;
                    var createdAtUtcSet = notification?.CreatedAtUtc != default(DateTime);
                    var readAtUtcNull = notification?.ReadAtUtc == null;

                    var allMatch = exists && userIdMatch && categoryMatch && titleMatch &&
                                   messageMatch && isReadFalse && createdAtUtcSet && readAtUtcNull;

                    return allMatch.Label(
                        $"Creation failed. Exists={exists}, " +
                        $"UserId={userIdMatch}, Category={categoryMatch}, " +
                        $"Title={titleMatch}, Message={messageMatch}, " +
                        $"IsRead=false:{isReadFalse}, CreatedAtUtcSet={createdAtUtcSet}, " +
                        $"ReadAtUtcNull={readAtUtcNull}");
                }
                finally
                {
                    dbContext.Dispose();
                    connection.Dispose();
                }
            });
    }

    /// <summary>
    /// Property: When a user has InAppEnabled=false for a category, creating a notification
    /// for that category SHALL NOT persist a Notification entity to the database.
    /// **Validates: Requirements 9.5**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property CreatingNotification_WhenInAppDisabled_DoesNotCreateEntity()
    {
        // Generator for random NotificationCategory values
        var categoryGen = Gen.Elements(Enum.GetValues<NotificationCategory>());

        return Prop.ForAll(
            Arb.From(categoryGen),
            (NotificationCategory category) =>
            {
                var (dbContext, connection) = CreateDbContext();
                try
                {
                    // Seed a valid user
                    var userId = SeedUser(dbContext);

                    // Seed a preference with InAppEnabled=false for the generated category
                    var preference = new NotificationPreference
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        Category = category,
                        InAppEnabled = false,
                        EmailEnabled = true
                    };
                    dbContext.NotificationPreferences.Add(preference);
                    dbContext.SaveChanges();

                    // Create the notification service and attempt to create a notification
                    var service = new NotificationService(dbContext, NullLogger<NotificationService>.Instance, null!);

                    var request = new CreateNotificationRequest
                    {
                        UserId = userId,
                        Category = category,
                        Title = "Test Notification",
                        Message = "This should not be persisted."
                    };

                    service.CreateNotificationAsync(request).GetAwaiter().GetResult();

                    // Verify no notification entity was created
                    var notificationCount = dbContext.Notifications.Count(n => n.UserId == userId);
                    return (notificationCount == 0).Label(
                        $"Expected 0 notifications when InAppEnabled=false, but found {notificationCount} for category '{category}'.");
                }
                finally
                {
                    dbContext.Dispose();
                    connection.Dispose();
                }
            });
    }

    /// <summary>
    /// Property: When a user has InAppEnabled=true for a category, creating a notification
    /// for that category SHALL persist a Notification entity to the database.
    /// **Validates: Requirements 9.5**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property CreatingNotification_WhenInAppEnabled_CreatesEntity()
    {
        // Generator for random NotificationCategory values
        var categoryGen = Gen.Elements(Enum.GetValues<NotificationCategory>());

        return Prop.ForAll(
            Arb.From(categoryGen),
            (NotificationCategory category) =>
            {
                var (dbContext, connection) = CreateDbContext();
                try
                {
                    // Seed a valid user
                    var userId = SeedUser(dbContext);

                    // Seed a preference with InAppEnabled=true for the generated category
                    var preference = new NotificationPreference
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        Category = category,
                        InAppEnabled = true,
                        EmailEnabled = true
                    };
                    dbContext.NotificationPreferences.Add(preference);
                    dbContext.SaveChanges();

                    // Create the notification service and attempt to create a notification
                    var service = new NotificationService(dbContext, NullLogger<NotificationService>.Instance, null!);

                    var request = new CreateNotificationRequest
                    {
                        UserId = userId,
                        Category = category,
                        Title = "Test Notification",
                        Message = "This should be persisted."
                    };

                    service.CreateNotificationAsync(request).GetAwaiter().GetResult();

                    // Verify a notification entity was created
                    var notificationCount = dbContext.Notifications.Count(n => n.UserId == userId);
                    return (notificationCount == 1).Label(
                        $"Expected 1 notification when InAppEnabled=true, but found {notificationCount} for category '{category}'.");
                }
                finally
                {
                    dbContext.Dispose();
                    connection.Dispose();
                }
            });
    }

    /// <summary>
    /// Property: When no preference record exists for a user-category pair, creating a notification
    /// SHALL persist a Notification entity to the database (default InAppEnabled=true).
    /// **Validates: Requirements 9.5**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property CreatingNotification_WhenNoPreferenceExists_CreatesEntity()
    {
        // Generator for random NotificationCategory values
        var categoryGen = Gen.Elements(Enum.GetValues<NotificationCategory>());

        return Prop.ForAll(
            Arb.From(categoryGen),
            (NotificationCategory category) =>
            {
                var (dbContext, connection) = CreateDbContext();
                try
                {
                    // Seed a valid user (no preferences seeded — default behavior)
                    var userId = SeedUser(dbContext);

                    // Create the notification service and attempt to create a notification
                    var service = new NotificationService(dbContext, NullLogger<NotificationService>.Instance, null!);

                    var request = new CreateNotificationRequest
                    {
                        UserId = userId,
                        Category = category,
                        Title = "Test Notification",
                        Message = "This should be persisted with default preferences."
                    };

                    service.CreateNotificationAsync(request).GetAwaiter().GetResult();

                    // Verify a notification entity was created (default InAppEnabled=true)
                    var notificationCount = dbContext.Notifications.Count(n => n.UserId == userId);
                    return (notificationCount == 1).Label(
                        $"Expected 1 notification when no preference exists (default true), but found {notificationCount} for category '{category}'.");
                }
                finally
                {
                    dbContext.Dispose();
                    connection.Dispose();
                }
            });
    }
}
