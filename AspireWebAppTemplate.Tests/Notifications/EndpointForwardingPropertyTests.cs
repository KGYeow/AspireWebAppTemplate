// Feature: notification-push-deep-link, Property 2: Endpoint forwards all parameters to SignalR
using AspireWebAppTemplate.Application.Contracts.Notifications;
using AspireWebAppTemplate.Domain.Enums;
using AspireWebAppTemplate.Web.Endpoints;
using AspireWebAppTemplate.Web.Hubs;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace AspireWebAppTemplate.Tests.Notifications;

/// <summary>
/// Property-based tests verifying that the <see cref="NotificationCallbackEndpoint"/> forwards
/// all parameters from a valid <see cref="NotificationPushRequest"/> to SignalR's
/// <c>SendAsync("ReceiveNotification")</c> with five arguments whose values exactly match:
/// Title, Message, Category, UnreadCount, and NotificationId.
/// </summary>
/// <remarks>
/// **Validates: Requirements 2.1**
/// </remarks>
public class EndpointForwardingPropertyTests
{
    /// <summary>
    /// The valid NotificationCategory string values that the endpoint accepts.
    /// </summary>
    private static readonly string[] ValidCategories =
        Enum.GetNames<NotificationCategory>();

    /// <summary>
    /// Property: For any valid NotificationPushRequest (non-empty UserId, non-empty Title,
    /// valid Category, UnreadCount >= 0, NotificationId != Guid.Empty), the endpoint SHALL
    /// invoke SignalR's SendAsync("ReceiveNotification") with five arguments whose values
    /// exactly match: request.Title, request.Message, request.Category, request.UnreadCount,
    /// and request.NotificationId.
    /// **Validates: Requirements 2.1**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property EndpointForwardsAllParametersToSignalR()
    {
        var userIdGen = Gen.Elements(
            "user1", "user-abc", "U123", "john.doe", "admin@corp.com");

        var titleGen = Gen.Elements(
            "New notification", "Alert", "System maintenance",
            "Short", "Title with spaces and 123 numbers");

        var messageGen = Gen.Elements(
            "", "Hello world", "Your account was updated",
            "A longer message body with details");

        var categoryGen = Gen.Elements(ValidCategories);

        var unreadCountGen = Gen.Choose(0, 10000);

        var notificationIdGen = Gen.Elements(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid());

        var requestGen = userIdGen.SelectMany(userId =>
            titleGen.SelectMany(title =>
                messageGen.SelectMany(message =>
                    categoryGen.SelectMany(category =>
                        unreadCountGen.SelectMany(count =>
                            notificationIdGen.Select(notificationId =>
                                new NotificationPushRequest
                                {
                                    UserId = userId,
                                    Title = title,
                                    Message = message,
                                    Category = category,
                                    UnreadCount = count,
                                    NotificationId = notificationId
                                }))))));

        return Prop.ForAll(
            Arb.From(requestGen),
            (NotificationPushRequest request) =>
            {
                // Arrange: capture the arguments passed to SendAsync
                object?[]? capturedArgs = null;
                string? capturedMethod = null;

                var mockClientProxy = new Mock<IClientProxy>();
                mockClientProxy
                    .Setup(c => c.SendCoreAsync(
                        It.IsAny<string>(),
                        It.IsAny<object?[]>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<string, object?[], CancellationToken>((method, args, _) =>
                    {
                        capturedMethod = method;
                        capturedArgs = args;
                    })
                    .Returns(Task.CompletedTask);

                var mockClients = new Mock<IHubClients>();
                mockClients
                    .Setup(c => c.Group(request.UserId))
                    .Returns(mockClientProxy.Object);

                var mockHubContext = new Mock<IHubContext<NotificationHub>>();
                mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);

                // Act
                var result = NotificationCallbackEndpoint.HandlePush(request, mockHubContext.Object)
                    .GetAwaiter().GetResult();

                // Assert: endpoint returned 200 OK
                var isOk = result is Ok;

                // Assert: SendAsync was invoked with "ReceiveNotification"
                var methodMatches = capturedMethod == "ReceiveNotification";

                // Assert: exactly 5 arguments were passed
                var hasCorrectArgCount = capturedArgs?.Length == 5;

                // Assert: all 5 arguments match the request properties
                var titleMatches = capturedArgs?[0]?.Equals(request.Title) ?? false;
                var messageMatches = capturedArgs?[1]?.Equals(request.Message) ?? false;
                var categoryMatches = capturedArgs?[2]?.Equals(request.Category) ?? false;
                var unreadCountMatches = capturedArgs?[3]?.Equals(request.UnreadCount) ?? false;
                var notificationIdMatches = capturedArgs?[4]?.Equals(request.NotificationId) ?? false;

                return (isOk && methodMatches && hasCorrectArgCount &&
                        titleMatches && messageMatches && categoryMatches &&
                        unreadCountMatches && notificationIdMatches)
                    .Label($"Expected all 5 args to match request. " +
                           $"Ok={isOk}, Method={capturedMethod}, ArgCount={capturedArgs?.Length}, " +
                           $"Title={titleMatches}, Message={messageMatches}, Category={categoryMatches}, " +
                           $"UnreadCount={unreadCountMatches}, NotificationId={notificationIdMatches}");
            });
    }
}
