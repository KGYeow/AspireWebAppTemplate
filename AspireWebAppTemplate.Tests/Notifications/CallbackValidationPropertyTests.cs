// Feature: realtime-notifications, Property 1: Valid callback requests are accepted
// Feature: realtime-notifications, Property 2: Invalid callback requests are rejected
using AspireWebAppTemplate.Core.Contracts.Notifications;
using AspireWebAppTemplate.Core.Domain.Enums;
using AspireWebAppTemplate.Web.Endpoints;
using AspireWebAppTemplate.Web.Hubs;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace AspireWebAppTemplate.Tests.Notifications;

/// <summary>
/// Property-based tests verifying that the notification callback endpoint correctly validates
/// incoming <see cref="NotificationPushRequest"/> payloads, accepting valid requests with 200 OK
/// and rejecting invalid requests with 400 Bad Request.
/// </summary>
/// <remarks>
/// **Validates: Requirements 2.2, 2.3, 2.8**
/// </remarks>
public class CallbackValidationPropertyTests
{
    /// <summary>
    /// The valid NotificationCategory string values that the endpoint accepts.
    /// </summary>
    private static readonly string[] ValidCategories =
        Enum.GetNames<NotificationCategory>();

    /// <summary>
    /// Creates a mock <see cref="IHubContext{NotificationHub}"/> that returns a no-op client proxy.
    /// </summary>
    private static IHubContext<NotificationHub> CreateMockHubContext()
    {
        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();

        mockClientProxy
            .Setup(c => c.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        mockClients
            .Setup(c => c.Group(It.IsAny<string>()))
            .Returns(mockClientProxy.Object);

        var mockHubContext = new Mock<IHubContext<NotificationHub>>();
        mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);

        return mockHubContext.Object;
    }

    /// <summary>
    /// Invokes the callback endpoint's handler directly with the given request.
    /// </summary>
    private static async Task<IResult> InvokeHandler(NotificationPushRequest request)
    {
        var hubContext = CreateMockHubContext();
        return await NotificationCallbackEndpoint.HandlePush(request, hubContext);
    }

    /// <summary>
    /// Property: For any NotificationPushRequest with a non-empty UserId, a non-empty Title of at most
    /// 200 characters, a valid NotificationCategory string value, and an UnreadCount >= 0, the callback
    /// endpoint SHALL return 200 OK.
    /// **Validates: Requirements 2.2, 2.3**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property ValidRequests_Return200Ok()
    {
        // Generator for non-empty, non-whitespace strings (for UserId)
        var nonEmptyStringGen = Gen.Elements(
            "user1", "user-abc", "U123", "john.doe", "admin@corp.com",
            "a", "user_with_underscores", "ID-999");

        // Generator for valid titles (1-200 chars, non-empty, non-whitespace)
        var titleGen = Gen.Elements(
            "New notification", "Alert", "A",
            "System maintenance scheduled for tonight",
            new string('X', 200),
            "Short", "Title with spaces and 123 numbers");

        var categoryGen = Gen.Elements(ValidCategories);

        var unreadCountGen = Gen.Choose(0, 10000);

        var notificationIdGen = Gen.Elements(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var requestGen = nonEmptyStringGen.SelectMany(userId =>
            titleGen.SelectMany(title =>
                categoryGen.SelectMany(category =>
                    unreadCountGen.SelectMany(count =>
                        notificationIdGen.Select(notificationId =>
                            new NotificationPushRequest
                            {
                                UserId = userId,
                                Title = title,
                                Category = category,
                                UnreadCount = count,
                                NotificationId = notificationId
                            })))));

        return Prop.ForAll(
            Arb.From(requestGen),
            (NotificationPushRequest request) =>
            {
                var result = InvokeHandler(request).GetAwaiter().GetResult();

                return (result is Ok).Label(
                    $"Expected 200 OK for valid request (UserId='{request.UserId}', " +
                    $"Title.Length={request.Title.Length}, Category='{request.Category}', " +
                    $"UnreadCount={request.UnreadCount}), but got {result.GetType().Name}");
            });
    }

    /// <summary>
    /// Property: For any NotificationPushRequest where UserId is empty/whitespace, the callback endpoint
    /// SHALL return 400 Bad Request.
    /// **Validates: Requirements 2.8**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property EmptyUserId_Returns400BadRequest()
    {
        var emptyUserIdGen = Gen.Elements("", " ", "  ", "\t", "\n");

        var titleGen = Gen.Elements("Valid Title", "Another Title", "Test");
        var categoryGen = Gen.Elements(ValidCategories);
        var unreadCountGen = Gen.Choose(0, 100);

        var requestGen = emptyUserIdGen.SelectMany(userId =>
            titleGen.SelectMany(title =>
                categoryGen.SelectMany(category =>
                    unreadCountGen.Select(count =>
                        new NotificationPushRequest
                        {
                            UserId = userId,
                            Title = title,
                            Category = category,
                            UnreadCount = count
                        }))));

        return Prop.ForAll(
            Arb.From(requestGen),
            (NotificationPushRequest request) =>
            {
                var result = InvokeHandler(request).GetAwaiter().GetResult();

                return (result is BadRequest<string>).Label(
                    $"Expected 400 Bad Request for empty UserId='{request.UserId}', " +
                    $"but got {result.GetType().Name}");
            });
    }

    /// <summary>
    /// Property: For any NotificationPushRequest where Title is empty/whitespace or exceeds 200 characters,
    /// the callback endpoint SHALL return 400 Bad Request.
    /// **Validates: Requirements 2.8**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property InvalidTitle_Returns400BadRequest()
    {
        // Generate titles that are either empty/whitespace or over 200 chars
        var emptyTitleGen = Gen.Elements("", " ", "  ", "\t");
        var longTitleGen = Gen.Choose(201, 500)
            .Select(len => new string('A', len));

        var invalidTitleGen = Gen.OneOf(emptyTitleGen, longTitleGen);

        var userIdGen = Gen.Elements("user1", "user2", "user-abc");
        var categoryGen = Gen.Elements(ValidCategories);
        var unreadCountGen = Gen.Choose(0, 100);

        var requestGen = userIdGen.SelectMany(userId =>
            invalidTitleGen.SelectMany(title =>
                categoryGen.SelectMany(category =>
                    unreadCountGen.Select(count =>
                        new NotificationPushRequest
                        {
                            UserId = userId,
                            Title = title,
                            Category = category,
                            UnreadCount = count
                        }))));

        return Prop.ForAll(
            Arb.From(requestGen),
            (NotificationPushRequest request) =>
            {
                var result = InvokeHandler(request).GetAwaiter().GetResult();

                return (result is BadRequest<string>).Label(
                    $"Expected 400 Bad Request for invalid Title (length={request.Title.Length}), " +
                    $"but got {result.GetType().Name}");
            });
    }

    /// <summary>
    /// Property: For any NotificationPushRequest where Category is not a valid NotificationCategory string,
    /// the callback endpoint SHALL return 400 Bad Request.
    /// **Validates: Requirements 2.8**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property InvalidCategory_Returns400BadRequest()
    {
        var invalidCategoryGen = Gen.Elements(
            "", " ", "Invalid", "unknown", "NotACategory", "system!", "xyz_category", "Systemm");

        var userIdGen = Gen.Elements("user1", "user2", "user-abc");
        var titleGen = Gen.Elements("Valid Title", "Another Title", "Test");
        var unreadCountGen = Gen.Choose(0, 100);

        var requestGen = userIdGen.SelectMany(userId =>
            titleGen.SelectMany(title =>
                invalidCategoryGen.SelectMany(category =>
                    unreadCountGen.Select(count =>
                        new NotificationPushRequest
                        {
                            UserId = userId,
                            Title = title,
                            Category = category,
                            UnreadCount = count
                        }))));

        return Prop.ForAll(
            Arb.From(requestGen),
            (NotificationPushRequest request) =>
            {
                var result = InvokeHandler(request).GetAwaiter().GetResult();

                return (result is BadRequest<string>).Label(
                    $"Expected 400 Bad Request for invalid Category='{request.Category}', " +
                    $"but got {result.GetType().Name}");
            });
    }

    /// <summary>
    /// Property: For any NotificationPushRequest where UnreadCount is negative, the callback endpoint
    /// SHALL return 400 Bad Request.
    /// **Validates: Requirements 2.8**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property NegativeUnreadCount_Returns400BadRequest()
    {
        var negativeCountGen = Gen.Choose(-10000, -1);

        var userIdGen = Gen.Elements("user1", "user2", "user-abc");
        var titleGen = Gen.Elements("Valid Title", "Another Title", "Test");
        var categoryGen = Gen.Elements(ValidCategories);

        var requestGen = userIdGen.SelectMany(userId =>
            titleGen.SelectMany(title =>
                categoryGen.SelectMany(category =>
                    negativeCountGen.Select(count =>
                        new NotificationPushRequest
                        {
                            UserId = userId,
                            Title = title,
                            Category = category,
                            UnreadCount = count
                        }))));

        return Prop.ForAll(
            Arb.From(requestGen),
            (NotificationPushRequest request) =>
            {
                var result = InvokeHandler(request).GetAwaiter().GetResult();

                return (result is BadRequest<string>).Label(
                    $"Expected 400 Bad Request for negative UnreadCount={request.UnreadCount}, " +
                    $"but got {result.GetType().Name}");
            });
    }
}
