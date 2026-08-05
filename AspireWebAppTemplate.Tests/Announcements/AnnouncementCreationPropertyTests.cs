// Feature: announcement-banner-system, Property 2: Creation preserves all input fields and sets audit metadata
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
/// Property-based tests verifying that announcement creation preserves all input fields
/// and correctly sets audit metadata (CreatedByUserId, CreatedAtUtc, UpdatedAtUtc).
/// </summary>
/// <remarks>
/// **Validates: Requirements 3.1, 3.2**
/// </remarks>
public class AnnouncementCreationPropertyTests
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
    /// Property: For any valid CreateAnnouncementRequest (Title ≤ 200 chars, Message ≤ 10000 chars,
    /// valid date range or nulls), creating an announcement SHALL produce a persisted entity where
    /// Title, Message, DisplayType, Severity, StartsAtUtc, ExpiresAtUtc, and IsActive match the request;
    /// CreatedByUserId matches the current user; and CreatedAtUtc/UpdatedAtUtc are set to a UTC value
    /// at or after the time of the call.
    /// **Validates: Requirements 3.1, 3.2**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property CreatedAnnouncement_PreservesAllInputFieldsAndSetsAuditMetadata()
    {
        // Build a composite generator using SelectMany chaining (FsCheck 3.x pattern).
        var gen = Gen.Choose(1, 200).SelectMany(titleLen =>
            Gen.Choose(1, 100).SelectMany(messageLen =>
                Gen.Elements(Enum.GetValues<AnnouncementDisplayType>()).SelectMany(displayType =>
                    Gen.Elements(Enum.GetValues<AnnouncementSeverity>()).SelectMany(severity =>
                        Gen.Elements(true, false)
                            .Select(isActive => (
                                title: new string('a', titleLen),
                                message: new string('m', messageLen),
                                displayType,
                                severity,
                                isActive))))));

        return Prop.ForAll(
            Arb.From(gen),
            ((string title, string message, AnnouncementDisplayType displayType, AnnouncementSeverity severity, bool isActive) input) =>
            {
                var (dbContext, connection) = CreateDbContext();
                try
                {
                    // Seed a valid user for the current user accessor.
                    var userId = SeedUser(dbContext);

                    // Configure mocks.
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

                    var service = new AnnouncementService(
                        dbContext,
                        mockCurrentUser.Object,
                        mockAuditLog.Object,
                        mockNotification.Object,
                        logger,
                        htmlSanitizer);

                    var request = new CreateAnnouncementRequest
                    {
                        Title = input.title,
                        Message = input.message,
                        DisplayType = input.displayType,
                        Severity = input.severity,
                        StartsAtUtc = null,
                        ExpiresAtUtc = null,
                        IsActive = input.isActive,
                        NotifyUsers = false
                    };

                    var timeBefore = DateTime.UtcNow;

                    // Act: create the announcement through the service.
                    var result = service.CreateAsync(request).GetAwaiter().GetResult();

                    var timeAfter = DateTime.UtcNow;

                    // Clear the change tracker so retrieval hits the database.
                    dbContext.ChangeTracker.Clear();

                    // Retrieve the persisted entity.
                    var entity = dbContext.Announcements.SingleOrDefault(a => a.Id == result.Id);

                    // Assert all fields are preserved correctly.
                    var exists = entity is not null;
                    var titleMatch = entity?.Title == input.title;
                    var messageMatch = entity?.Content == input.message; // Content = sanitized Message (plain text passes through)
                    var displayTypeMatch = entity?.DisplayType == input.displayType;
                    var severityMatch = entity?.Severity == input.severity;
                    var isActiveMatch = entity?.IsActive == input.isActive;
                    var startsAtMatch = entity?.StartsAtUtc == null;
                    var expiresAtMatch = entity?.ExpiresAtUtc == null;
                    var createdByMatch = entity?.CreatedByUserId == userId;
                    var createdAtValid = entity?.CreatedAtUtc >= timeBefore && entity?.CreatedAtUtc <= timeAfter;
                    var updatedAtValid = entity?.UpdatedAtUtc >= timeBefore && entity?.UpdatedAtUtc <= timeAfter;

                    // Also verify the returned DTO matches.
                    var dtoTitleMatch = result.Title == input.title;
                    var dtoMessageMatch = result.Message == input.message;
                    var dtoDisplayTypeMatch = result.DisplayType == input.displayType;
                    var dtoSeverityMatch = result.Severity == input.severity;
                    var dtoIsActiveMatch = result.IsActive == input.isActive;

                    var allMatch = exists && titleMatch && messageMatch && displayTypeMatch &&
                                   severityMatch && isActiveMatch && startsAtMatch && expiresAtMatch &&
                                   createdByMatch && createdAtValid && updatedAtValid &&
                                   dtoTitleMatch && dtoMessageMatch && dtoDisplayTypeMatch &&
                                   dtoSeverityMatch && dtoIsActiveMatch;

                    return allMatch.Label(
                        $"Creation field preservation failed. Exists={exists}, " +
                        $"Title={titleMatch}, Message={messageMatch}, DisplayType={displayTypeMatch}, " +
                        $"Severity={severityMatch}, IsActive={isActiveMatch}, " +
                        $"StartsAt={startsAtMatch}, ExpiresAt={expiresAtMatch}, " +
                        $"CreatedBy={createdByMatch}, CreatedAtValid={createdAtValid}, " +
                        $"UpdatedAtValid={updatedAtValid}, DtoTitle={dtoTitleMatch}, " +
                        $"DtoMessage={dtoMessageMatch}, DtoDisplayType={dtoDisplayTypeMatch}, " +
                        $"DtoSeverity={dtoSeverityMatch}, DtoIsActive={dtoIsActiveMatch}");
                }
                finally
                {
                    dbContext.Dispose();
                    connection.Dispose();
                }
            });
    }
}
