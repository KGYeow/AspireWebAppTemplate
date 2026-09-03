// Feature: announcement-banner-system, Property 1: Status classification is consistent with IsActive, StartsAtUtc, and ExpiresAtUtc
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
/// Property-based tests verifying that the computed status of an announcement is consistent
/// with the combination of IsActive, StartsAtUtc, ExpiresAtUtc, and the reference UTC time.
/// Tests through the public GetAllAsync API which maps entities to DTOs with computed Status.
/// </summary>
/// <remarks>
/// **Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5**
/// </remarks>
public class AnnouncementStatusPropertyTests
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
            SecurityStamp = Guid.NewGuid().ToString()
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
    /// Computes the expected status for an announcement given its fields and a reference time.
    /// This mirrors the specification's priority-ordered classification logic.
    /// </summary>
    private static string ComputeExpectedStatus(bool isActive, DateTime? startsAtUtc, DateTime? expiresAtUtc, DateTime referenceTime)
    {
        if (expiresAtUtc is not null && referenceTime >= expiresAtUtc)
            return "Expired";

        if (startsAtUtc is not null && referenceTime < startsAtUtc)
            return "Scheduled";

        if (isActive)
            return "Active";

        return "Draft";
    }

    /// <summary>
    /// Property: For any announcement with arbitrary IsActive, StartsAtUtc, ExpiresAtUtc, the computed
    /// status returned by GetAllAsync SHALL match the expected classification based on the evaluation
    /// order: Expired → Scheduled → Active → Draft.
    /// **Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property StatusClassification_IsConsistentWithFields()
    {
        // Generator for IsActive boolean
        var isActiveGen = Gen.Elements(true, false);

        // Generator for nullable DateTime offsets (relative to a base time)
        // Using offsets in hours to keep times manageable
        var nullableDateTimeOffsetGen = Gen.OneOf(
            Gen.Constant<int?>(null),
            Gen.Choose(-720, 720).Select(h => (int?)h)
        );

        return Prop.ForAll(
            Arb.From(isActiveGen),
            Arb.From(nullableDateTimeOffsetGen),
            Arb.From(nullableDateTimeOffsetGen),
            (bool isActive, int? startsAtOffset, int? expiresAtOffset) =>
            {
                var (dbContext, connection) = CreateDbContext();
                try
                {
                    var userId = SeedUser(dbContext);

                    // Use a fixed reference time; offsets create StartsAtUtc/ExpiresAtUtc relative to it
                    var referenceTime = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);
                    var startsAtUtc = startsAtOffset.HasValue
                        ? referenceTime.AddHours(startsAtOffset.Value)
                        : (DateTime?)null;
                    var expiresAtUtc = expiresAtOffset.HasValue
                        ? referenceTime.AddHours(expiresAtOffset.Value)
                        : (DateTime?)null;

                    // Seed the announcement entity directly into the database
                    var announcement = new Announcement
                    {
                        Id = Guid.NewGuid(),
                        Title = "Test Announcement",
                        Content = "<p>Test content</p>",
                        DisplayType = AnnouncementDisplayType.Banner,
                        Severity = AnnouncementSeverity.Info,
                        StartsAtUtc = startsAtUtc,
                        ExpiresAtUtc = expiresAtUtc,
                        IsActive = isActive,
                        NotifyUsers = false,
                        CreatedByUserId = userId,
                        CreatedAtUtc = referenceTime.AddDays(-1),
                        UpdatedAtUtc = referenceTime.AddDays(-1)
                    };
                    dbContext.Announcements.Add(announcement);
                    dbContext.SaveChanges();
                    dbContext.ChangeTracker.Clear();

                    // Act: call GetAllAsync which computes the Status via MapToDto → ComputeStatus
                    var service = CreateService(dbContext, userId);
                    var results = service.GetAllAsync().GetAwaiter().GetResult();

                    // Find our announcement in the results
                    var dto = results.SingleOrDefault(a => a.Id == announcement.Id);
                    var expectedStatus = ComputeExpectedStatus(isActive, startsAtUtc, expiresAtUtc, referenceTime);

                    // The service uses DateTime.UtcNow internally, so we verify the logic
                    // by computing what the expected status WOULD be at the current time
                    var utcNow = DateTime.UtcNow;
                    var expectedAtNow = ComputeExpectedStatus(isActive, startsAtUtc, expiresAtUtc, utcNow);

                    var statusMatches = dto is not null && dto.Status == expectedAtNow;

                    return statusMatches.Label(
                        $"Status mismatch. Expected='{expectedAtNow}', Actual='{dto?.Status}', " +
                        $"IsActive={isActive}, StartsAtUtc={startsAtUtc}, ExpiresAtUtc={expiresAtUtc}, " +
                        $"Now={utcNow:O}");
                }
                finally
                {
                    dbContext.Dispose();
                    connection.Dispose();
                }
            });
    }
}
