// Feature: notification-push-deep-link, Property 3: Hub event parameters faithfully populate event args
using System.Net;
using System.Reflection;
using System.Text;
using AspireWebAppTemplate.Web.Common;
using AspireWebAppTemplate.Web.Services;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

namespace AspireWebAppTemplate.Tests.Notifications;

/// <summary>
/// Property-based tests verifying that the NotificationContext hub handler faithfully
/// populates NotificationReceivedEventArgs from the received hub parameters.
/// </summary>
/// <remarks>
/// **Validates: Requirements 3.3**
/// </remarks>
public class NotificationContextEventArgsPropertyTests
{
    /// <summary>
    /// Creates a mocked ApiNotificationService that returns a fixed unread count.
    /// </summary>
    private static ApiNotificationService CreateMockedApiService(int unreadCount = 0)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(unreadCount.ToString(), Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://localhost")
        };

        return new ApiNotificationService(httpClient);
    }

    /// <summary>
    /// Property: For any tuple of (title, message, category, unreadCount, notificationId) received
    /// by the NotificationContext hub handler, the raised OnNotificationReceived event args SHALL have
    /// Title == title, Message == message, Category == category, and NotificationId == notificationId.
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property HandleReceiveNotification_PopulatesEventArgs_FromHubParameters()
    {
        // Generate random tuples of (title, message, category, unreadCount, notificationId)
        // using FsCheck 3.x SelectMany chaining pattern.
        var gen = Gen.Elements("Alert", "Reminder", "Update", "Welcome").SelectMany(title =>
            Gen.Elements("You have a new message", "Action required", "Task completed").SelectMany(message =>
                Gen.Elements("Account", "Activity", "System").SelectMany(category =>
                    Gen.Choose(0, 1000).SelectMany(unreadCount =>
                        Gen.Elements(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())
                            .Select(notificationId => (title, message, category, unreadCount, notificationId))))));

        return Prop.ForAll(
            Arb.From(gen),
            ((string title, string message, string category, int unreadCount, Guid notificationId) input) =>
            {
                var (title, message, category, unreadCount, notificationId) = input;

                // Arrange: create a NotificationContext instance
                var apiService = CreateMockedApiService();
                var logger = NullLogger<NotificationContext>.Instance;
                var context = new NotificationContext(
                    apiService,
                    logger,
                    new Mock<IHttpContextAccessor>().Object,
                    new Mock<ApiAuthService>(new HttpClient()).Object);

                // Subscribe to the OnNotificationReceived event to capture args
                NotificationReceivedEventArgs? capturedArgs = null;
                context.OnNotificationReceived += args => capturedArgs = args;

                // Act: invoke the private HandleReceiveNotification method via reflection
                var method = typeof(NotificationContext).GetMethod(
                    "HandleReceiveNotification",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                method!.Invoke(context, [title, message, category, unreadCount, notificationId]);

                // Assert: event args faithfully reflect the hub parameters
                var titleMatch = capturedArgs?.Title == title;
                var messageMatch = capturedArgs?.Message == message;
                var categoryMatch = capturedArgs?.Category == category;
                var idMatch = capturedArgs?.NotificationId == notificationId;

                return (capturedArgs is not null && titleMatch && messageMatch && categoryMatch && idMatch).Label(
                    $"Expected args with Title='{title}', Message='{message}', Category='{category}', " +
                    $"NotificationId={notificationId}, but got " +
                    $"Title='{capturedArgs?.Title}', Message='{capturedArgs?.Message}', " +
                    $"Category='{capturedArgs?.Category}', NotificationId={capturedArgs?.NotificationId}");
            });
    }
}
