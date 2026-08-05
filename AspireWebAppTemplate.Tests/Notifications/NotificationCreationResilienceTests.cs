// Feature: realtime-notifications, Property 3: Callback failure does not disrupt notification creation
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Data.Entities;
using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Infrastructure.Services;
using AspireWebAppTemplate.Infrastructure.Clients;
using AspireWebAppTemplate.Application.Contracts.Notifications;
using AspireWebAppTemplate.Domain.Enums;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

namespace AspireWebAppTemplate.Tests.Notifications;

/// <summary>
/// Property-based tests verifying that callback failures do not disrupt notification creation.
/// The NotificationService must persist the notification to the database and complete without
/// throwing regardless of what failure mode the WebCallbackClient encounters.
/// </summary>
/// <remarks>
/// **Validates: Requirements 2.7**
/// </remarks>
public class NotificationCreationResilienceTests
{
    /// <summary>
    /// Enumerates the failure modes that the WebCallbackClient can encounter during notification delivery.
    /// </summary>
    private enum CallbackFailureMode
    {
        /// <summary>HTTP 500 Internal Server Error response.</summary>
        Http500,
        /// <summary>HTTP 503 Service Unavailable response.</summary>
        Http503,
        /// <summary>Request timeout (TaskCanceledException).</summary>
        Timeout,
        /// <summary>Network-level failure (HttpRequestException).</summary>
        NetworkException
    }

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
    /// Creates a WebCallbackClient backed by a mock HttpMessageHandler configured to
    /// simulate the specified failure mode.
    /// </summary>
    private static WebCallbackClient CreateFailingWebCallbackClient(CallbackFailureMode failureMode)
    {
        var mockHandler = new Mock<HttpMessageHandler>();

        switch (failureMode)
        {
            case CallbackFailureMode.Http500:
                mockHandler.Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .ReturnsAsync(new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError));
                break;

            case CallbackFailureMode.Http503:
                mockHandler.Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .ReturnsAsync(new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable));
                break;

            case CallbackFailureMode.Timeout:
                mockHandler.Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .ThrowsAsync(new TaskCanceledException("The request timed out."));
                break;

            case CallbackFailureMode.NetworkException:
                mockHandler.Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .ThrowsAsync(new HttpRequestException("Network unreachable."));
                break;
        }

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost")
        };

        return new WebCallbackClient(httpClient, NullLogger<WebCallbackClient>.Instance);
    }

    /// <summary>
    /// Property: For any notification creation request and any callback failure mode
    /// (HTTP 500, HTTP 503, timeout, network exception), the NotificationService.CreateNotificationAsync
    /// method SHALL still successfully persist the notification to the database and complete without throwing.
    /// **Validates: Requirements 2.7**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property CallbackFailure_DoesNotDisrupt_NotificationCreation()
    {
        // Generate a random callback failure mode and a random notification category.
        var failureModeGen = Gen.Elements(
            CallbackFailureMode.Http500,
            CallbackFailureMode.Http503,
            CallbackFailureMode.Timeout,
            CallbackFailureMode.NetworkException);

        var categoryGen = Gen.Elements(Enum.GetValues<NotificationCategory>());

        var combinedGen = failureModeGen.SelectMany(fm => categoryGen.Select(cat => (fm, cat)));

        return Prop.ForAll(
            Arb.From(combinedGen),
            ((CallbackFailureMode failureMode, NotificationCategory category) input) =>
            {
                var (dbContext, connection) = CreateDbContext();
                try
                {
                    // Seed a valid user so the service's user-exists check passes.
                    var userId = SeedUser(dbContext);

                    // Create a WebCallbackClient that simulates the given failure mode.
                    var failingClient = CreateFailingWebCallbackClient(input.failureMode);

                    var service = new NotificationService(
                        dbContext,
                        NullLogger<NotificationService>.Instance,
                        failingClient);

                    var request = new CreateNotificationRequest
                    {
                        UserId = userId,
                        Category = input.category,
                        Title = $"Resilience test - {input.failureMode}",
                        Message = $"Testing that {input.failureMode} does not disrupt creation."
                    };

                    // Act: create the notification — should NOT throw.
                    Exception? caughtException = null;
                    try
                    {
                        service.CreateNotificationAsync(request).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        caughtException = ex;
                    }

                    // Assert: no exception propagated.
                    var noException = caughtException is null;

                    // Assert: notification was persisted to the database.
                    dbContext.ChangeTracker.Clear();
                    var notification = dbContext.Notifications
                        .SingleOrDefault(n => n.UserId == userId);

                    var persisted = notification is not null;
                    var titleMatch = notification?.Title == request.Title;
                    var categoryMatch = notification?.Category == request.Category;

                    var allPassed = noException && persisted && titleMatch && categoryMatch;

                    return allPassed.Label(
                        $"FailureMode={input.failureMode}, Category={input.category}: " +
                        $"NoException={noException}, Persisted={persisted}, " +
                        $"TitleMatch={titleMatch}, CategoryMatch={categoryMatch}" +
                        (caughtException is not null ? $", Exception={caughtException.GetType().Name}: {caughtException.Message}" : ""));
                }
                finally
                {
                    dbContext.Dispose();
                    connection.Dispose();
                }
            });
    }
}
