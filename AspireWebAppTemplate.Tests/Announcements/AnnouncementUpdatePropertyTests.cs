// Feature: announcement-banner-system, Property 4: Update preserves fields and refreshes UpdatedAtUtc
using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Application.Features.Template.Notifications;
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Data.Entities.Template;
using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Infrastructure.Services;
using AspireWebAppTemplate.Infrastructure.Services.Template.Announcements;
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
/// Property-based tests verifying that updating an announcement preserves all input fields
/// from the update request, refreshes UpdatedAtUtc, and leaves CreatedAtUtc and CreatedByUserId unchanged.
/// </summary>
/// <remarks>
/// **Validates: Requirements 4.1**
/// </remarks>
public class AnnouncementUpdatePropertyTests
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
    /// Property: For any existing announcement and valid UpdateAnnouncementRequest,
    /// updating SHALL modify the entity fields to match the request and set UpdatedAtUtc
    /// to a UTC value at or after the time of the call, while preserving CreatedAtUtc
    /// and CreatedByUserId unchanged.
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property UpdatedAnnouncement_PreservesFieldsAndRefreshesUpdatedAtUtc()
    {
        // Generate random update fields (title length 1-200, message length 1-100, random enums, random isActive).
        var gen = Gen.Choose(1, 200).SelectMany(titleLen =>
            Gen.Choose(1, 100).SelectMany(messageLen =>
                Gen.Elements(Enum.GetValues<AnnouncementDisplayType>()).SelectMany(displayType =>
                    Gen.Elements(Enum.GetValues<AnnouncementSeverity>()).SelectMany(severity =>
                        Gen.Elements(true, false)
                            .Select(isActive => (
                                title: new string('t', titleLen),
                                message: new string('u', messageLen),
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
                    mockNotification.Setup(x => x.CreateNotificationAsync(It.IsAny<Application.Features.Template.Notifications.CreateNotificationRequest>()))
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

                    // Create an announcement first to have an existing entity to update.
                    var createRequest = new CreateAnnouncementRequest
                    {
                        Title = "Original Title",
                        Message = "Original message content",
                        DisplayType = AnnouncementDisplayType.Standard,
                        Severity = AnnouncementSeverity.Info,
                        StartsAtUtc = null,
                        ExpiresAtUtc = null,
                        IsActive = true,
                        NotifyUsers = false
                    };

                    var created = service.CreateAsync(createRequest).GetAwaiter().GetResult();
                    var originalCreatedAtUtc = created.CreatedAtUtc;

                    // Clear the change tracker to ensure we read from DB.
                    dbContext.ChangeTracker.Clear();

                    // Now update with generated random fields.
                    var updateRequest = new UpdateAnnouncementRequest
                    {
                        Title = input.title,
                        Message = input.message,
                        DisplayType = input.displayType,
                        Severity = input.severity,
                        StartsAtUtc = null,
                        ExpiresAtUtc = null,
                        IsActive = input.isActive,
                        NotifyUsers = false,
                        ClearDismissals = false
                    };

                    var timeBeforeUpdate = DateTime.UtcNow;

                    var updated = service.UpdateAsync(created.Id, updateRequest).GetAwaiter().GetResult();

                    var timeAfterUpdate = DateTime.UtcNow;

                    // Clear the change tracker so retrieval hits the database.
                    dbContext.ChangeTracker.Clear();

                    // Retrieve the persisted entity.
                    var entity = dbContext.Announcements.SingleOrDefault(a => a.Id == created.Id);

                    // Assert updated fields match the update request.
                    var exists = entity is not null;
                    var titleMatch = entity?.Title == input.title;
                    var contentMatch = entity?.Content == input.message; // Plain text passes through sanitizer unchanged.
                    var displayTypeMatch = entity?.DisplayType == input.displayType;
                    var severityMatch = entity?.Severity == input.severity;
                    var isActiveMatch = entity?.IsActive == input.isActive;

                    // Assert UpdatedAtUtc is refreshed to at or after timeBeforeUpdate.
                    var updatedAtValid = entity?.UpdatedAtUtc >= timeBeforeUpdate && entity?.UpdatedAtUtc <= timeAfterUpdate;

                    // Assert CreatedAtUtc is preserved unchanged.
                    var createdAtPreserved = entity?.CreatedAtUtc == originalCreatedAtUtc;

                    // Assert CreatedByUserId is preserved unchanged.
                    var createdByPreserved = entity?.CreatedByUserId == userId;

                    // Also verify the returned DTO matches.
                    var dtoTitleMatch = updated.Title == input.title;
                    var dtoMessageMatch = updated.Message == input.message;
                    var dtoDisplayTypeMatch = updated.DisplayType == input.displayType;
                    var dtoSeverityMatch = updated.Severity == input.severity;
                    var dtoIsActiveMatch = updated.IsActive == input.isActive;

                    var allMatch = exists && titleMatch && contentMatch && displayTypeMatch &&
                                   severityMatch && isActiveMatch && updatedAtValid &&
                                   createdAtPreserved && createdByPreserved &&
                                   dtoTitleMatch && dtoMessageMatch && dtoDisplayTypeMatch &&
                                   dtoSeverityMatch && dtoIsActiveMatch;

                    return allMatch.Label(
                        $"Update field preservation failed. Exists={exists}, " +
                        $"Title={titleMatch}, Content={contentMatch}, DisplayType={displayTypeMatch}, " +
                        $"Severity={severityMatch}, IsActive={isActiveMatch}, " +
                        $"UpdatedAtValid={updatedAtValid}, CreatedAtPreserved={createdAtPreserved}, " +
                        $"CreatedByPreserved={createdByPreserved}, DtoTitle={dtoTitleMatch}, " +
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
