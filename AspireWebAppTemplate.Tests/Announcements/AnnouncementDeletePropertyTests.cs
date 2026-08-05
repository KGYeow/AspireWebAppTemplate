// Feature: announcement-banner-system, Property 6: Delete removes announcement and all associated dismissals
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
/// Property-based tests verifying that deleting an announcement removes both the
/// announcement entity and all associated dismissal records from the database.
/// </summary>
/// <remarks>
/// **Validates: Requirements 5.1**
/// </remarks>
public class AnnouncementDeletePropertyTests
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

        // Disable FK enforcement so we can test announcement deletion
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
    /// Property: For any announcement with N ≥ 0 associated dismissal records, deleting SHALL
    /// remove both the announcement entity and all N dismissal records from the database.
    /// **Validates: Requirements 5.1**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property DeleteAnnouncement_RemovesAnnouncementAndAllDismissals()
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
                    mockNotification.Setup(x => x.CreateNotificationAsync(It.IsAny<Application.Contracts.Notifications.CreateNotificationRequest>()))
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
                    var request = new CreateAnnouncementRequest
                    {
                        Title = "Test Delete Announcement",
                        Message = "Content for delete test",
                        DisplayType = AnnouncementDisplayType.Banner,
                        Severity = AnnouncementSeverity.Info,
                        StartsAtUtc = null,
                        ExpiresAtUtc = null,
                        IsActive = true,
                        NotifyUsers = false
                    };

                    var created = service.CreateAsync(request).GetAwaiter().GetResult();
                    var announcementId = created.Id;

                    // Seed N dismissal records for distinct users.
                    for (int i = 0; i < dismissalCount; i++)
                    {
                        var userId = SeedUser(dbContext);
                        var dismissal = new AnnouncementDismissal
                        {
                            UserId = userId,
                            AnnouncementId = announcementId,
                            DismissedAtUtc = DateTime.UtcNow
                        };
                        dbContext.AnnouncementDismissals.Add(dismissal);
                    }
                    dbContext.SaveChanges();

                    // Verify dismissals were seeded.
                    var dismissalsBefore = dbContext.AnnouncementDismissals
                        .Count(d => d.AnnouncementId == announcementId);
                    var dismissalsSeeded = dismissalsBefore == dismissalCount;

                    // Act: delete the announcement.
                    service.DeleteAsync(announcementId).GetAwaiter().GetResult();

                    // Clear the change tracker so retrieval hits the database.
                    dbContext.ChangeTracker.Clear();

                    // Assert: announcement is gone.
                    var announcementGone = !dbContext.Announcements.Any(a => a.Id == announcementId);

                    // Assert: all dismissal records are gone.
                    var dismissalsGone = !dbContext.AnnouncementDismissals.Any(d => d.AnnouncementId == announcementId);

                    var allPass = dismissalsSeeded && announcementGone && dismissalsGone;

                    return allPass.Label(
                        $"Delete cascade failed. DismissalCount={dismissalCount}, " +
                        $"DismissalsSeeded={dismissalsSeeded}, " +
                        $"AnnouncementGone={announcementGone}, DismissalsGone={dismissalsGone}");
                }
                finally
                {
                    dbContext.Dispose();
                    connection.Dispose();
                }
            });
    }
}
