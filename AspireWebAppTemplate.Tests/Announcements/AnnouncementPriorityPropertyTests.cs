// Feature: announcement-banner-system, Property 8: Priority ordering selects by Severity descending then CreatedAtUtc descending
using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Application.Features.Template.Notifications;
using AspireWebAppTemplate.Application.Features.Template.AuditLog;
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Data.Entities;
using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Infrastructure.Services;
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
/// Property-based tests verifying that priority ordering places announcements by Severity
/// descending (Critical > Warning > Info) and, within the same severity, by CreatedAtUtc
/// descending (newest first). Tested through the GetActiveForUserAsync public API.
/// </summary>
/// <remarks>
/// **Validates: Requirements 7.1, 7.2, 9.5**
/// </remarks>
public class AnnouncementPriorityPropertyTests
{
    /// <summary>
    /// Creates a SQLite in-memory ApplicationDbContext for testing.
    /// Foreign key enforcement is disabled to allow seeding entities without satisfying
    /// all relational constraints.
    /// </summary>
    private static (ApplicationDbContext dbContext, SqliteConnection connection) CreateDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

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
            SecurityStamp = Guid.NewGuid().ToString(),
            IsActive = true
        };
        dbContext.Users.Add(user);
        dbContext.SaveChanges();
        return userId;
    }

    /// <summary>
    /// Creates a configured AnnouncementService with mocked dependencies.
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
    /// Property: For any set of active announcements with varied severities and creation dates,
    /// GetActiveForUserAsync SHALL return them ordered by Severity descending (Critical > Warning > Info),
    /// then by CreatedAtUtc descending (newer before older within the same severity).
    /// **Validates: Requirements 7.1, 7.2, 9.5**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property PriorityOrdering_SeverityDescendingThenCreatedAtDescending()
    {
        // Generator for a list of (severity, createdAtOffsetHours) tuples representing announcements
        var severityGen = Gen.Elements(
            AnnouncementSeverity.Info,
            AnnouncementSeverity.Warning,
            AnnouncementSeverity.Critical);

        // Generate a single announcement entry as a tuple
        var announcementEntryGen = severityGen.SelectMany(sev =>
            Gen.Choose(1, 1000).Select(offset => (severity: sev, createdAtOffset: offset)));

        // Generate 2 to 6 announcements with random severity and unique creation time offsets
        var announcementListGen = Gen.Choose(2, 6).SelectMany<int, (AnnouncementSeverity severity, int createdAtOffset)[]>(count =>
            Gen.ArrayOf(announcementEntryGen, count));

        return Prop.ForAll(
            Arb.From(announcementListGen),
            ((AnnouncementSeverity severity, int createdAtOffset)[] announcements) =>
            {
                var (dbContext, connection) = CreateDbContext();
                try
                {
                    var userId = SeedUser(dbContext);
                    var baseTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

                    // Seed active announcements with varied severities and creation times
                    var seededIds = new List<(Guid Id, AnnouncementSeverity Severity, DateTime CreatedAtUtc)>();
                    foreach (var (severity, offset) in announcements)
                    {
                        var createdAt = baseTime.AddHours(offset);
                        var announcement = new Announcement
                        {
                            Id = Guid.NewGuid(),
                            Title = $"Announcement {severity} {offset}",
                            Content = "<p>Test</p>",
                            DisplayType = AnnouncementDisplayType.Banner,
                            Severity = severity,
                            StartsAtUtc = null,
                            ExpiresAtUtc = null,
                            IsActive = true,
                            NotifyUsers = false,
                            CreatedByUserId = userId,
                            CreatedAtUtc = createdAt,
                            UpdatedAtUtc = createdAt
                        };
                        dbContext.Announcements.Add(announcement);
                        seededIds.Add((announcement.Id, severity, createdAt));
                    }
                    dbContext.SaveChanges();
                    dbContext.ChangeTracker.Clear();

                    // Act: call GetActiveForUserAsync which applies priority ordering
                    var service = CreateService(dbContext, userId);
                    var results = service.GetActiveForUserAsync(userId).GetAwaiter().GetResult();

                    // Verify ordering: each consecutive pair must satisfy the priority rule
                    var orderCorrect = true;
                    for (int i = 0; i < results.Count - 1; i++)
                    {
                        var current = results[i];
                        var next = results[i + 1];

                        // Severity must be >= (descending: Critical=2 > Warning=1 > Info=0)
                        if (current.Severity < next.Severity)
                        {
                            orderCorrect = false;
                            break;
                        }

                        // Within the same severity, CreatedAtUtc must be >= (descending: newer first)
                        if (current.Severity == next.Severity && current.CreatedAtUtc < next.CreatedAtUtc)
                        {
                            orderCorrect = false;
                            break;
                        }
                    }

                    return orderCorrect.Label(
                        $"Priority ordering violated. Results: [{string.Join(", ", results.Select(r => $"{r.Severity}:{r.CreatedAtUtc:HH:mm}"))}]");
                }
                finally
                {
                    dbContext.Dispose();
                    connection.Dispose();
                }
            });
    }
}
