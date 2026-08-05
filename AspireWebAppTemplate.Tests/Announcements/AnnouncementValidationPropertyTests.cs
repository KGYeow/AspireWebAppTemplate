// Feature: announcement-banner-system, Property 3: Creation rejects invalid input
using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Data.Entities;
using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Infrastructure.Services;
using AspireWebAppTemplate.Application.Contracts.Announcements;
using AspireWebAppTemplate.Application.Contracts.AuditLog;
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
/// Property-based tests verifying that announcement creation rejects invalid input
/// (title too long, message too long, or invalid date range) with a descriptive validation
/// error and no entity persisted.
/// </summary>
/// <remarks>
/// **Validates: Requirements 3.3, 3.4**
/// </remarks>
public class AnnouncementValidationPropertyTests
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

        // Disable FK enforcement so we can test announcement creation
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
    /// Creates a configured AnnouncementService instance with mocked dependencies.
    /// </summary>
    private static AnnouncementService CreateService(ApplicationDbContext dbContext, string userId)
    {
        var mockCurrentUser = new Mock<ICurrentUserAccessor>();
        mockCurrentUser.Setup(x => x.UserId).Returns(userId);
        mockCurrentUser.Setup(x => x.UserName).Returns("testadmin");
        mockCurrentUser.Setup(x => x.IpAddress).Returns("127.0.0.1");

        var mockAuditLog = new Mock<IAuditLogService>();
        mockAuditLog.Setup(x => x.LogAsync(It.IsAny<AuditLogRequest>())).Returns(Task.CompletedTask);

        var mockNotification = new Mock<INotificationService>();
        mockNotification.Setup(x => x.CreateNotificationAsync(It.IsAny<Application.Contracts.Notifications.CreateNotificationRequest>()))
            .Returns(Task.CompletedTask);

        var logger = NullLogger<AnnouncementService>.Instance;
        var htmlSanitizer = new HtmlSanitizer();

        return new AnnouncementService(
            dbContext,
            mockCurrentUser.Object,
            mockAuditLog.Object,
            mockNotification.Object,
            logger,
            htmlSanitizer);
    }

    /// <summary>
    /// Property: For any CreateAnnouncementRequest with a Title exceeding 200 characters,
    /// the service SHALL reject the request with an ArgumentException and no entity shall be persisted.
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property CreateAnnouncement_WithTitleTooLong_ThrowsAndDoesNotPersist()
    {
        // Generator for title lengths that exceed the 200-character limit (201 to 300).
        var longTitleLenGen = Gen.Choose(201, 300);

        return Prop.ForAll(
            Arb.From(longTitleLenGen),
            (int titleLen) =>
            {
                var (dbContext, connection) = CreateDbContext();
                try
                {
                    var userId = SeedUser(dbContext);
                    var service = CreateService(dbContext, userId);

                    var request = new CreateAnnouncementRequest
                    {
                        Title = new string('x', titleLen),
                        Message = "Valid message content",
                        DisplayType = AnnouncementDisplayType.Standard,
                        Severity = AnnouncementSeverity.Info,
                        StartsAtUtc = null,
                        ExpiresAtUtc = null,
                        IsActive = true,
                        NotifyUsers = false
                    };

                    // Act: attempt to create with invalid title.
                    var threw = false;
                    try
                    {
                        service.CreateAsync(request).GetAwaiter().GetResult();
                    }
                    catch (ArgumentException)
                    {
                        threw = true;
                    }

                    // Verify no entity was persisted.
                    var count = dbContext.Announcements.Count();

                    var rejected = threw && count == 0;
                    return rejected.Label(
                        $"Title length={titleLen}: Threw={threw}, EntityCount={count}");
                }
                finally
                {
                    dbContext.Dispose();
                    connection.Dispose();
                }
            });
    }

    /// <summary>
    /// Property: For any CreateAnnouncementRequest with a Message exceeding 10000 characters,
    /// the service SHALL reject the request with an ArgumentException and no entity shall be persisted.
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property CreateAnnouncement_WithMessageTooLong_ThrowsAndDoesNotPersist()
    {
        // Generator for message lengths that exceed the 10000-character limit (10001 to 10100).
        var longMessageLenGen = Gen.Choose(10001, 10100);

        return Prop.ForAll(
            Arb.From(longMessageLenGen),
            (int messageLen) =>
            {
                var (dbContext, connection) = CreateDbContext();
                try
                {
                    var userId = SeedUser(dbContext);
                    var service = CreateService(dbContext, userId);

                    var request = new CreateAnnouncementRequest
                    {
                        Title = "Valid Title",
                        Message = new string('y', messageLen),
                        DisplayType = AnnouncementDisplayType.Banner,
                        Severity = AnnouncementSeverity.Warning,
                        StartsAtUtc = null,
                        ExpiresAtUtc = null,
                        IsActive = false,
                        NotifyUsers = false
                    };

                    // Act: attempt to create with invalid message.
                    var threw = false;
                    try
                    {
                        service.CreateAsync(request).GetAwaiter().GetResult();
                    }
                    catch (ArgumentException)
                    {
                        threw = true;
                    }

                    // Verify no entity was persisted.
                    var count = dbContext.Announcements.Count();

                    var rejected = threw && count == 0;
                    return rejected.Label(
                        $"Message length={messageLen}: Threw={threw}, EntityCount={count}");
                }
                finally
                {
                    dbContext.Dispose();
                    connection.Dispose();
                }
            });
    }

    /// <summary>
    /// Property: For any CreateAnnouncementRequest where StartsAtUtc >= ExpiresAtUtc (both provided),
    /// the service SHALL reject the request with an ArgumentException and no entity shall be persisted.
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property CreateAnnouncement_WithInvalidDateRange_ThrowsAndDoesNotPersist()
    {
        // Generator for a non-negative offset in hours (0 means equal, positive means starts > expires).
        var offsetGen = Gen.Choose(0, 48);

        return Prop.ForAll(
            Arb.From(offsetGen),
            (int offsetHours) =>
            {
                var (dbContext, connection) = CreateDbContext();
                try
                {
                    var userId = SeedUser(dbContext);
                    var service = CreateService(dbContext, userId);

                    // StartsAtUtc >= ExpiresAtUtc: start is baseDate + offset, expires is baseDate
                    var baseDate = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);
                    var expiresAt = baseDate;
                    var startsAt = baseDate.AddHours(offsetHours);

                    var request = new CreateAnnouncementRequest
                    {
                        Title = "Valid Title",
                        Message = "Valid message content",
                        DisplayType = AnnouncementDisplayType.Standard,
                        Severity = AnnouncementSeverity.Critical,
                        StartsAtUtc = startsAt,
                        ExpiresAtUtc = expiresAt,
                        IsActive = true,
                        NotifyUsers = false
                    };

                    // Act: attempt to create with invalid date range.
                    var threw = false;
                    try
                    {
                        service.CreateAsync(request).GetAwaiter().GetResult();
                    }
                    catch (ArgumentException)
                    {
                        threw = true;
                    }

                    // Verify no entity was persisted.
                    var count = dbContext.Announcements.Count();

                    var rejected = threw && count == 0;
                    return rejected.Label(
                        $"StartsAt={startsAt:o} >= ExpiresAt={expiresAt:o} (offset={offsetHours}h): Threw={threw}, EntityCount={count}");
                }
                finally
                {
                    dbContext.Dispose();
                    connection.Dispose();
                }
            });
    }
}
