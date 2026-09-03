// Feature: notification-push-deep-link, Property 1: Push request carries persisted entity ID
using System.Net;
using System.Text.Json;
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Data.Entities;
using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Infrastructure.Services;
using AspireWebAppTemplate.Infrastructure.Services.Template.Notifications;
using AspireWebAppTemplate.Infrastructure.Clients;
using AspireWebAppTemplate.Application.Features.Template.Notifications;
using AspireWebAppTemplate.Domain.Enums;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AspireWebAppTemplate.Tests.Notifications;

/// <summary>
/// Property-based tests verifying that the NotificationPushRequest sent to
/// WebCallbackClient.NotifyAsync carries the persisted notification entity's ID.
/// </summary>
/// <remarks>
/// **Validates: Requirements 1.2**
/// </remarks>
public class NotificationPushIdPropertyTests
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
    /// Seeds an InAppEnabled=true notification preference for the given user and category.
    /// </summary>
    private static void SeedPreference(ApplicationDbContext dbContext, string userId, NotificationCategory category)
    {
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
    }

    /// <summary>
    /// A delegating handler that captures the HTTP request body content for inspection.
    /// Returns a 200 OK response without actually sending the request over the network.
    /// </summary>
    private sealed class CapturingHandler : DelegatingHandler
    {
        /// <summary>
        /// The captured NotificationPushRequest from the most recent POST request.
        /// </summary>
        public NotificationPushRequest? CapturedRequest { get; private set; }

        /// <summary>
        /// Intercepts the HTTP request, deserializes the JSON body into a NotificationPushRequest,
        /// and returns a 200 OK response.
        /// </summary>
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Content is not null)
            {
                var json = await request.Content.ReadAsStringAsync(cancellationToken);
                CapturedRequest = JsonSerializer.Deserialize<NotificationPushRequest>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    /// <summary>
    /// Property: For any valid CreateNotificationRequest that passes all guards (user exists,
    /// InApp enabled), the NotificationPushRequest sent to WebCallbackClient.NotifyAsync SHALL
    /// have its NotificationId property equal to the Id of the newly persisted Notification entity.
    /// **Validates: Requirements 1.2**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property PushRequest_CarriesPersistedEntityId()
    {
        // Generator for NotificationCategory enum values.
        var categoryGen = Gen.Elements(Enum.GetValues<NotificationCategory>());

        return Prop.ForAll(
            Arb.From(categoryGen),
            (NotificationCategory category) =>
            {
                var (dbContext, connection) = CreateDbContext();
                var capturingHandler = new CapturingHandler();
                try
                {
                    // Seed a valid user and an InAppEnabled preference.
                    var userId = SeedUser(dbContext);
                    SeedPreference(dbContext, userId, category);

                    // Create a WebCallbackClient with a capturing HTTP handler.
                    var httpClient = new HttpClient(capturingHandler)
                    {
                        BaseAddress = new Uri("http://localhost")
                    };
                    var webCallbackClient = new WebCallbackClient(
                        httpClient, NullLogger<WebCallbackClient>.Instance);

                    var logger = NullLogger<NotificationService>.Instance;
                    var service = new NotificationService(dbContext, logger, webCallbackClient);

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

                    // Retrieve the persisted notification entity.
                    var notification = dbContext.Notifications
                        .SingleOrDefault(n => n.UserId == userId);

                    // Assert the captured push request's NotificationId matches the entity's Id.
                    var entityExists = notification is not null;
                    var pushCaptured = capturingHandler.CapturedRequest is not null;
                    var idMatches = entityExists && pushCaptured &&
                                    capturingHandler.CapturedRequest!.NotificationId == notification!.Id;

                    return idMatches.Label(
                        $"Push NotificationId mismatch. " +
                        $"EntityExists={entityExists}, PushCaptured={pushCaptured}, " +
                        $"EntityId={notification?.Id}, PushNotificationId={capturingHandler.CapturedRequest?.NotificationId}");
                }
                finally
                {
                    capturingHandler.Dispose();
                    dbContext.Dispose();
                    connection.Dispose();
                }
            });
    }
}
