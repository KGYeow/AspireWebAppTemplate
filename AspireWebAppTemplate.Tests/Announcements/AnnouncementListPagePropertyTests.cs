// Feature: announcement-banner-system, Property 12: List page query includes active plus expired within 30 days
using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Application.Features.Template.Notifications;
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Data.Entities.Template;
using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Infrastructure.Services;
using AspireWebAppTemplate.Infrastructure.Services.Template.Announcements;
using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Application.Features.Template.Announcements;
using AspireWebAppTemplate.Application.Features.Template.AuditLog;
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
/// Property-based tests verifying that the list page query (GetForListPageAsync) returns
/// all currently active announcements plus announcements expired within the last 30 days,
/// but excludes announcements expired more than 30 days ago.
/// </summary>
/// <remarks>
/// **Validates: Requirements 10.2**
/// </remarks>
public class AnnouncementListPagePropertyTests
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
    /// Property: For any set of announcements with varying expiry dates, the list page query
    /// (GetForListPageAsync) SHALL return all currently active announcements AND announcements
    /// whose ExpiresAtUtc is within the last 30 days, but SHALL NOT return announcements
    /// expired more than 30 days ago.
    /// **Validates: Requirements 10.2**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property GetForListPageAsync_ReturnsActiveAndRecentlyExpired_ExcludesOldExpired()
    {
        // Generate days-ago values for expired announcements: one within 30 days, one beyond.
        var gen = Gen.Choose(1, 29).SelectMany<int, (int recentDaysAgo, int oldDaysAgo)>(recentDaysAgo =>
            Gen.Choose(31, 60).Select(oldDaysAgo => (recentDaysAgo, oldDaysAgo)));

        return Prop.ForAll(
            Arb.From(gen),
            ((int recentDaysAgo, int oldDaysAgo) input) =>
            {
                var (dbContext, connection) = CreateDbContext();
                try
                {
                    var userId = SeedUser(dbContext);
                    var utcNow = DateTime.UtcNow;
                    var baseTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

                    // 1. Active announcement (no expiry, IsActive=true)
                    var activeNoExpiry = new Announcement
                    {
                        Id = Guid.NewGuid(),
                        Title = "Active No Expiry",
                        Content = "<p>Active</p>",
                        DisplayType = AnnouncementDisplayType.Banner,
                        Severity = AnnouncementSeverity.Info,
                        StartsAtUtc = null,
                        ExpiresAtUtc = null,
                        IsActive = true,
                        NotifyUsers = false,
                        CreatedByUserId = userId,
                        CreatedAtUtc = baseTime,
                        UpdatedAtUtc = baseTime
                    };

                    // 2. Active announcement (future expiry)
                    var activeFutureExpiry = new Announcement
                    {
                        Id = Guid.NewGuid(),
                        Title = "Active Future Expiry",
                        Content = "<p>Active Future</p>",
                        DisplayType = AnnouncementDisplayType.Standard,
                        Severity = AnnouncementSeverity.Warning,
                        StartsAtUtc = null,
                        ExpiresAtUtc = utcNow.AddDays(10),
                        IsActive = true,
                        NotifyUsers = false,
                        CreatedByUserId = userId,
                        CreatedAtUtc = baseTime.AddHours(1),
                        UpdatedAtUtc = baseTime.AddHours(1)
                    };

                    // 3. Expired within 30 days (should be included)
                    var recentlyExpired = new Announcement
                    {
                        Id = Guid.NewGuid(),
                        Title = "Recently Expired",
                        Content = "<p>Expired recently</p>",
                        DisplayType = AnnouncementDisplayType.Banner,
                        Severity = AnnouncementSeverity.Critical,
                        StartsAtUtc = null,
                        ExpiresAtUtc = utcNow.AddDays(-input.recentDaysAgo),
                        IsActive = true,
                        NotifyUsers = false,
                        CreatedByUserId = userId,
                        CreatedAtUtc = baseTime.AddHours(2),
                        UpdatedAtUtc = baseTime.AddHours(2)
                    };

                    // 4. Expired more than 30 days ago (should be excluded)
                    var oldExpired = new Announcement
                    {
                        Id = Guid.NewGuid(),
                        Title = "Old Expired",
                        Content = "<p>Expired long ago</p>",
                        DisplayType = AnnouncementDisplayType.Standard,
                        Severity = AnnouncementSeverity.Info,
                        StartsAtUtc = null,
                        ExpiresAtUtc = utcNow.AddDays(-input.oldDaysAgo),
                        IsActive = true,
                        NotifyUsers = false,
                        CreatedByUserId = userId,
                        CreatedAtUtc = baseTime.AddHours(3),
                        UpdatedAtUtc = baseTime.AddHours(3)
                    };

                    dbContext.Announcements.AddRange(activeNoExpiry, activeFutureExpiry, recentlyExpired, oldExpired);
                    dbContext.SaveChanges();
                    dbContext.ChangeTracker.Clear();

                    // Configure service with mocks.
                    var mockCurrentUser = new Mock<ICurrentUserAccessor>();
                    mockCurrentUser.Setup(x => x.UserId).Returns(userId);
                    mockCurrentUser.Setup(x => x.UserName).Returns("testadmin");
                    mockCurrentUser.Setup(x => x.IpAddress).Returns("127.0.0.1");

                    var mockAuditLog = new Mock<IAuditLogService>();
                    var mockNotification = new Mock<INotificationService>();
                    var logger = NullLogger<AnnouncementService>.Instance;
                    var htmlSanitizer = new HtmlSanitizer();

                    var service = new AnnouncementService(
                        dbContext,
                        mockCurrentUser.Object,
                        mockAuditLog.Object,
                        mockNotification.Object,
                        logger,
                        htmlSanitizer);

                    // Act: call GetForListPageAsync.
                    var results = service.GetForListPageAsync(new AnnouncementQueryParams { Page = 1, PageSize = 50 }).GetAwaiter().GetResult();
                    var resultIds = results.Items.Select(r => r.Id).ToHashSet();

                    // Verify: active announcements are included.
                    var activeNoExpiryIncluded = resultIds.Contains(activeNoExpiry.Id);
                    var activeFutureIncluded = resultIds.Contains(activeFutureExpiry.Id);

                    // Verify: recently expired (within 30 days) is included.
                    var recentlyExpiredIncluded = resultIds.Contains(recentlyExpired.Id);

                    // Verify: old expired (more than 30 days) is excluded.
                    var oldExpiredExcluded = !resultIds.Contains(oldExpired.Id);

                    var allCorrect = activeNoExpiryIncluded && activeFutureIncluded &&
                                     recentlyExpiredIncluded && oldExpiredExcluded;

                    return allCorrect.Label(
                        $"List page 30-day window failed. ActiveNoExpiry={activeNoExpiryIncluded}, " +
                        $"ActiveFuture={activeFutureIncluded}, RecentExpired={recentlyExpiredIncluded} " +
                        $"(expired {input.recentDaysAgo} days ago), OldExpiredExcluded={oldExpiredExcluded} " +
                        $"(expired {input.oldDaysAgo} days ago). ResultCount={results.Items.Count}");
                }
                finally
                {
                    dbContext.Dispose();
                    connection.Dispose();
                }
            });
    }
}
