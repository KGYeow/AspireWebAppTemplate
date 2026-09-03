// Feature: announcement-banner-system, Property 14: Notification delivery respects NotifyUsers flag
using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Data.Entities.Template;
using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Infrastructure.Services;
using AspireWebAppTemplate.Infrastructure.Services.Template.Announcements;
using AspireWebAppTemplate.Application.Features.Template.Announcements;
using AspireWebAppTemplate.Application.Features.Template.AuditLog;
using AspireWebAppTemplate.Application.Features.Template.Notifications;
using AspireWebAppTemplate.Domain.Enums;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Ganss.Xss;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AspireWebAppTemplate.Tests.Announcements;

/// <summary>
/// Property-based tests verifying that notification delivery respects the NotifyUsers flag
/// on announcement creation. When NotifyUsers=true and the announcement is immediately active,
/// a notification is created for each active user. When NotifyUsers=false, no notifications
/// are created regardless of other fields.
/// </summary>
/// <remarks>
/// **Validates: Requirements 16.1, 16.4, 16.5**
/// </remarks>
public class AnnouncementNotificationPropertyTests
{
    /// <summary>
    /// Creates a SQLite in-memory ApplicationDbContext for testing.
    /// Foreign key enforcement is disabled to allow seeding minimal entities
    /// without satisfying all relational constraints.
    /// </summary>
    private static (ApplicationDbContext dbContext, SqliteConnection connection) CreateDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        // Disable FK enforcement so we can test announcement operations
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
    /// The user is seeded with IsActive=true by default.
    /// </summary>
    private static string SeedUser(ApplicationDbContext dbContext, bool isActive = true)
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
            SecurityStamp = Guid.NewGuid().ToString(),
            IsActive = isActive
        };
        dbContext.Users.Add(user);
        dbContext.SaveChanges();
        return userId;
    }

    /// <summary>
    /// Property: For any announcement with NotifyUsers=true, IsActive=true, and StartsAtUtc=null
    /// (immediately active), creating the announcement SHALL invoke CreateNotificationAsync
    /// exactly once per active user in the system. For any announcement with NotifyUsers=false,
    /// CreateNotificationAsync SHALL NOT be invoked regardless of other fields.
    /// **Validates: Requirements 16.1, 16.4, 16.5**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property CreateAsync_RespectsNotifyUsersFlag()
    {
        // Generate active user count (1-3) and NotifyUsers flag.
        var gen = Gen.Choose(1, 3).SelectMany<int, (int activeUserCount, bool notifyUsers)>(userCount =>
            Gen.Elements(true, false).Select(notify => (userCount, notify)));

        return Prop.ForAll(
            Arb.From(gen),
            ((int activeUserCount, bool notifyUsers) input) =>
            {
                var (dbContext, connection) = CreateDbContext();
                try
                {
                    // Seed the admin user (who creates the announcement).
                    var adminUserId = SeedUser(dbContext);

                    // Seed N additional active users who should receive notifications.
                    var activeUserIds = new List<string>();
                    for (int i = 0; i < input.activeUserCount; i++)
                    {
                        activeUserIds.Add(SeedUser(dbContext, isActive: true));
                    }

                    // Configure mocks.
                    var mockCurrentUser = new Mock<ICurrentUserAccessor>();
                    mockCurrentUser.Setup(x => x.UserId).Returns(adminUserId);
                    mockCurrentUser.Setup(x => x.UserName).Returns("testadmin");
                    mockCurrentUser.Setup(x => x.IpAddress).Returns("127.0.0.1");

                    var mockAuditLog = new Mock<IAuditLogService>();
                    mockAuditLog.Setup(x => x.LogAsync(It.IsAny<AuditLogRequest>())).Returns(Task.CompletedTask);

                    var mockNotification = new Mock<INotificationService>();
                    mockNotification.Setup(x => x.CreateNotificationAsync(It.IsAny<CreateNotificationRequest>()))
                        .Returns(Task.CompletedTask);

                    var logger = NullLogger<AnnouncementService>.Instance;
                    var htmlSanitizer = new HtmlSanitizer();

                    var service = new AnnouncementService(
                        dbContext,
                        mockCurrentUser.Object,
                        mockAuditLog.Object,
                        mockNotification.Object,
                        logger,
                        htmlSanitizer);

                    var request = new CreateAnnouncementRequest
                    {
                        Title = "Notification Test Announcement",
                        Message = "Testing notification delivery",
                        DisplayType = AnnouncementDisplayType.Standard,
                        Severity = AnnouncementSeverity.Info,
                        StartsAtUtc = null,
                        ExpiresAtUtc = null,
                        IsActive = true,
                        NotifyUsers = input.notifyUsers
                    };

                    // Act: create the announcement.
                    service.CreateAsync(request).GetAwaiter().GetResult();

                    if (input.notifyUsers)
                    {
                        // When NotifyUsers=true: notifications should be created for ALL active users
                        // (admin + additional users = activeUserCount + 1 total active users).
                        var expectedNotificationCount = input.activeUserCount + 1; // admin is also active
                        mockNotification.Verify(
                            x => x.CreateNotificationAsync(It.IsAny<CreateNotificationRequest>()),
                            Times.Exactly(expectedNotificationCount));

                        return true.Label(
                            $"NotifyUsers=true: Expected {expectedNotificationCount} notifications " +
                            $"for {input.activeUserCount + 1} active users.");
                    }
                    else
                    {
                        // When NotifyUsers=false: no notifications should be created.
                        mockNotification.Verify(
                            x => x.CreateNotificationAsync(It.IsAny<CreateNotificationRequest>()),
                            Times.Never);

                        return true.Label(
                            $"NotifyUsers=false: Verified no notifications created.");
                    }
                }
                finally
                {
                    dbContext.Dispose();
                    connection.Dispose();
                }
            });
    }
}
