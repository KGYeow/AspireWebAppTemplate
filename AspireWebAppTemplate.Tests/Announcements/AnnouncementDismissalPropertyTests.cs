// Feature: announcement-banner-system, Property 5: ClearDismissals removes all dismissal records for the announcement
// Feature: announcement-banner-system, Property 9: Dismissal excludes announcement from user's banner query
// Feature: announcement-banner-system, Property 10: Dismissal is idempotent
using AspireWebAppTemplate.Abstractions;
using AspireWebAppTemplate.ApiService.Abstractions;
using AspireWebAppTemplate.ApiService.Data;
using AspireWebAppTemplate.ApiService.Data.Entities;
using AspireWebAppTemplate.ApiService.Services;
using AspireWebAppTemplate.Core.Contracts.Announcements;
using AspireWebAppTemplate.Core.Contracts.AuditLog;
using AspireWebAppTemplate.Core.Domain.Enums;
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
/// Property-based tests verifying that updating an announcement with ClearDismissals=true
/// removes all associated dismissal records for that announcement, and that dismissed
/// announcements are excluded from the user's active banner query.
/// </summary>
/// <remarks>
/// **Validates: Requirements 4.3, 8.1, 8.3**
/// </remarks>
public class AnnouncementDismissalPropertyTests
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
    /// Property: For any announcement with N >= 0 associated dismissal records, updating
    /// with ClearDismissals=true SHALL result in zero dismissal records for that announcement
    /// after the operation completes.
    /// **Validates: Requirements 4.3**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property ClearDismissals_RemovesAllDismissalRecords()
    {
        // Generate a random number of dismissals (0 to 5).
        var gen = Gen.Choose(0, 5);

        return Prop.ForAll(
            Arb.From(gen),
            (int dismissalCount) =>
            {
                var (dbContext, connection) = CreateDbContext();
                try
                {
                    // Seed the admin user who creates the announcement.
                    var adminUserId = SeedUser(dbContext);

                    // Configure mocks.
                    var mockCurrentUser = new Mock<ICurrentUserAccessor>();
                    mockCurrentUser.Setup(x => x.UserId).Returns(adminUserId);
                    mockCurrentUser.Setup(x => x.UserName).Returns("testadmin");
                    mockCurrentUser.Setup(x => x.IpAddress).Returns("127.0.0.1");

                    var mockAuditLog = new Mock<IAuditLogService>();
                    mockAuditLog.Setup(x => x.LogAsync(It.IsAny<AuditLogRequest>())).Returns(Task.CompletedTask);

                    var mockNotification = new Mock<INotificationService>();
                    mockNotification.Setup(x => x.CreateNotificationAsync(It.IsAny<Core.Contracts.Notifications.CreateNotificationRequest>()))
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

                    // Create an announcement via the service.
                    var createRequest = new CreateAnnouncementRequest
                    {
                        Title = "Test Announcement",
                        Message = "Test content for dismissal clearing",
                        DisplayType = AnnouncementDisplayType.Banner,
                        Severity = AnnouncementSeverity.Info,
                        StartsAtUtc = null,
                        ExpiresAtUtc = null,
                        IsActive = true,
                        NotifyUsers = false
                    };

                    var created = service.CreateAsync(createRequest).GetAwaiter().GetResult();

                    // Seed N dismissal records directly into the database.
                    for (var i = 0; i < dismissalCount; i++)
                    {
                        var dismissalUserId = SeedUser(dbContext);
                        var dismissal = new AnnouncementDismissal
                        {
                            UserId = dismissalUserId,
                            AnnouncementId = created.Id,
                            DismissedAtUtc = DateTime.UtcNow.AddMinutes(-i)
                        };
                        dbContext.AnnouncementDismissals.Add(dismissal);
                    }
                    dbContext.SaveChanges();

                    // Verify the dismissals were seeded correctly.
                    dbContext.ChangeTracker.Clear();
                    var dismissalsBefore = dbContext.AnnouncementDismissals
                        .Count(d => d.AnnouncementId == created.Id);
                    var seededCorrectly = dismissalsBefore == dismissalCount;

                    // Now update with ClearDismissals=true.
                    var updateRequest = new UpdateAnnouncementRequest
                    {
                        Title = "Updated Title",
                        Message = "Updated content",
                        DisplayType = AnnouncementDisplayType.Banner,
                        Severity = AnnouncementSeverity.Warning,
                        StartsAtUtc = null,
                        ExpiresAtUtc = null,
                        IsActive = true,
                        NotifyUsers = false,
                        ClearDismissals = true
                    };

                    service.UpdateAsync(created.Id, updateRequest).GetAwaiter().GetResult();

                    // Clear the change tracker and verify zero dismissals remain.
                    dbContext.ChangeTracker.Clear();
                    var dismissalsAfter = dbContext.AnnouncementDismissals
                        .Count(d => d.AnnouncementId == created.Id);
                    var allCleared = dismissalsAfter == 0;

                    var result = seededCorrectly && allCleared;

                    return result.Label(
                        $"ClearDismissals failed. DismissalCount={dismissalCount}, " +
                        $"SeededCorrectly={seededCorrectly} (before={dismissalsBefore}), " +
                        $"AllCleared={allCleared} (after={dismissalsAfter})");
                }
                finally
                {
                    dbContext.Dispose();
                    connection.Dispose();
                }
            });
    }

    /// <summary>
    /// Creates a configured AnnouncementService with mocked dependencies for query/dismissal operations.
    /// </summary>
    private static AnnouncementService CreateService(ApplicationDbContext dbContext, string userId)
    {
        var mockCurrentUser = new Mock<ICurrentUserAccessor>();
        mockCurrentUser.Setup(x => x.UserId).Returns(userId);
        mockCurrentUser.Setup(x => x.UserName).Returns("testadmin");
        mockCurrentUser.Setup(x => x.IpAddress).Returns("127.0.0.1");

        var mockAuditLog = new Mock<IAuditLogService>();
        var mockNotification = new Mock<INotificationService>();
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
    /// Property: For any user and N active Banner-type announcements (N from 1 to 4),
    /// after dismissing a random subset via DismissAsync, calling GetActiveForUserAsync
    /// SHALL NOT include dismissed announcements and SHALL include non-dismissed announcements.
    /// **Validates: Requirements 8.1, 8.3**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property DismissedAnnouncements_ExcludedFromBannerQuery()
    {
        // Generator for (count, dismissMask) where count is 1-4 and mask is which to dismiss
        var inputGen = Gen.Choose(1, 4).SelectMany<int, (int count, bool[] mask)>(count =>
            Gen.ArrayOf(Gen.Elements(true, false), count)
                .Select(mask => (count, mask)));

        return Prop.ForAll(
            Arb.From(inputGen),
            ((int count, bool[] mask) input) =>
            {
                var (dbContext, connection) = CreateDbContext();
                try
                {
                    var userId = SeedUser(dbContext);
                    var baseTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

                    // Seed N active Banner-type announcements
                    var seededIds = new List<Guid>();
                    for (int i = 0; i < input.count; i++)
                    {
                        var announcement = new Announcement
                        {
                            Id = Guid.NewGuid(),
                            Title = $"Banner Announcement {i}",
                            Content = "<p>Test content</p>",
                            DisplayType = AnnouncementDisplayType.Banner,
                            Severity = AnnouncementSeverity.Info,
                            StartsAtUtc = null,
                            ExpiresAtUtc = null,
                            IsActive = true,
                            NotifyUsers = false,
                            CreatedByUserId = userId,
                            CreatedAtUtc = baseTime.AddHours(i),
                            UpdatedAtUtc = baseTime.AddHours(i)
                        };
                        dbContext.Announcements.Add(announcement);
                        seededIds.Add(announcement.Id);
                    }
                    dbContext.SaveChanges();
                    dbContext.ChangeTracker.Clear();

                    // Dismiss a subset based on the generated mask
                    var service = CreateService(dbContext, userId);
                    var dismissedIds = new HashSet<Guid>();
                    var nonDismissedIds = new HashSet<Guid>();

                    for (int i = 0; i < input.count; i++)
                    {
                        if (input.mask[i])
                        {
                            service.DismissAsync(userId, seededIds[i]).GetAwaiter().GetResult();
                            dismissedIds.Add(seededIds[i]);
                        }
                        else
                        {
                            nonDismissedIds.Add(seededIds[i]);
                        }
                    }

                    dbContext.ChangeTracker.Clear();

                    // Act: query active announcements for the user
                    var results = service.GetActiveForUserAsync(userId).GetAwaiter().GetResult();
                    var resultIds = results.Select(r => r.Id).ToHashSet();

                    // Verify: dismissed announcements are NOT in results
                    var dismissedExcluded = dismissedIds.All(id => !resultIds.Contains(id));

                    // Verify: non-dismissed announcements ARE in results
                    var nonDismissedIncluded = nonDismissedIds.All(id => resultIds.Contains(id));

                    return (dismissedExcluded && nonDismissedIncluded).Label(
                        $"Dismissed excluded: {dismissedExcluded}, Non-dismissed included: {nonDismissedIncluded}. " +
                        $"Dismissed: [{string.Join(", ", dismissedIds)}], " +
                        $"Non-dismissed: [{string.Join(", ", nonDismissedIds)}], " +
                        $"Results: [{string.Join(", ", resultIds)}]");
                }
                finally
                {
                    dbContext.Dispose();
                    connection.Dispose();
                }
            });
    }

    /// <summary>
    /// Property: For any user-announcement pair, dismissing the same announcement N times
    /// (N from 2 to 5) SHALL result in exactly one dismissal record in the database and
    /// complete successfully without error on each call.
    /// **Validates: Requirements 8.4**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property DismissAsync_IsIdempotent_SingleRecordAfterMultipleCalls()
    {
        // Generate N repetitions (2 to 5) for idempotent dismissal testing.
        var gen = Gen.Choose(2, 5);

        return Prop.ForAll(
            Arb.From(gen),
            (int dismissCount) =>
            {
                var (dbContext, connection) = CreateDbContext();
                try
                {
                    var userId = SeedUser(dbContext);
                    var baseTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

                    // Seed a single active announcement.
                    var announcementId = Guid.NewGuid();
                    var announcement = new Announcement
                    {
                        Id = announcementId,
                        Title = "Idempotent Dismissal Test",
                        Content = "<p>Test content</p>",
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
                    dbContext.Announcements.Add(announcement);
                    dbContext.SaveChanges();
                    dbContext.ChangeTracker.Clear();

                    var service = CreateService(dbContext, userId);

                    // Act: dismiss the same announcement N times without error.
                    var allSucceeded = true;
                    for (int i = 0; i < dismissCount; i++)
                    {
                        try
                        {
                            service.DismissAsync(userId, announcementId).GetAwaiter().GetResult();
                        }
                        catch
                        {
                            allSucceeded = false;
                            break;
                        }
                    }

                    dbContext.ChangeTracker.Clear();

                    // Verify: exactly one dismissal record exists.
                    var dismissalRecords = dbContext.AnnouncementDismissals
                        .Count(d => d.AnnouncementId == announcementId && d.UserId == userId);
                    var exactlyOne = dismissalRecords == 1;

                    return (allSucceeded && exactlyOne).Label(
                        $"Idempotent dismissal failed. DismissCount={dismissCount}, " +
                        $"AllSucceeded={allSucceeded}, DismissalRecords={dismissalRecords}");
                }
                finally
                {
                    dbContext.Dispose();
                    connection.Dispose();
                }
            });
    }
}
