// Feature: notification-system, Property 6: NotificationContext cache correctly reflects mark/dismiss operations
using System.Net;
using System.Text;
using AspireWebAppTemplate.Web.Services;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

namespace AspireWebAppTemplate.Tests.Notifications;

/// <summary>
/// Property-based tests verifying that NotificationContext correctly caches and updates
/// the unread notification count through sequences of DecrementCount and ClearCount operations.
/// </summary>
/// <remarks>
/// **Validates: Requirements 3.4**
/// </remarks>
public class NotificationContextPropertyTests
{
    /// <summary>
    /// Creates a mocked HttpMessageHandler that returns the specified unread count
    /// for GET /api/notifications/unread-count requests.
    /// </summary>
    /// <param name="unreadCount">The unread count to return from the mock API.</param>
    /// <returns>An ApiNotificationService backed by the mocked handler.</returns>
    private static ApiNotificationService CreateMockedApiService(int unreadCount)
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
    /// Property: After InitializeAsync with a mocked unread count N, and applying a sequence of
    /// DecrementCount operations with random amounts, the final UnreadCount SHALL equal
    /// max(0, N - sum_of_decrements). The count never goes below zero.
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property DecrementCount_ClampsToZero_AfterSequenceOfDecrements()
    {
        // Generate initial unread count between 1 and 50,
        // then combine with a list of 1–10 decrement amounts (each 1–5).
        var gen = Gen.Choose(1, 50).SelectMany(initialCount =>
            Gen.Choose(1, 5).ListOf()
                .Select(list => list.Take(10).ToList())
                .Where(list => list.Count >= 1 && list.Count <= 10)
                .Select(decrements => (initialCount, decrements)));

        return Prop.ForAll(
            Arb.From(gen),
            ((int initialCount, List<int> decrements) input) =>
            {
                var (initialCount, decrements) = input;

                // Arrange: create context with mocked API returning the initial count
                var apiService = CreateMockedApiService(initialCount);
                var logger = NullLogger<NotificationContext>.Instance;
                var context = new NotificationContext(apiService, logger);

                // Act: initialize to load the count from the "API"
                context.InitializeAsync().GetAwaiter().GetResult();

                // Apply all decrements in sequence
                foreach (var amount in decrements)
                {
                    context.DecrementCount(amount);
                }

                // Assert: final count equals max(0, initial - total_decrements)
                var totalDecrements = decrements.Sum();
                var expectedCount = Math.Max(0, initialCount - totalDecrements);
                var actualCount = context.UnreadCount;

                return (actualCount == expectedCount).Label(
                    $"Expected UnreadCount={expectedCount} (initial={initialCount}, " +
                    $"totalDecrements={totalDecrements}), but got {actualCount}");
            });
    }

    /// <summary>
    /// Property: After InitializeAsync with a mocked unread count N, calling ClearCount()
    /// SHALL set UnreadCount to 0 regardless of the initial count value.
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property ClearCount_SetsUnreadCountToZero()
    {
        // Generate initial unread count between 1 and 50
        var initialCountGen = Gen.Choose(1, 50);

        return Prop.ForAll(
            Arb.From(initialCountGen),
            (int initialCount) =>
            {
                // Arrange: create context with mocked API returning the initial count
                var apiService = CreateMockedApiService(initialCount);
                var logger = NullLogger<NotificationContext>.Instance;
                var context = new NotificationContext(apiService, logger);

                // Act: initialize to load the count, then clear
                context.InitializeAsync().GetAwaiter().GetResult();
                context.ClearCount();

                // Assert: count is always 0 after ClearCount
                var actualCount = context.UnreadCount;

                return (actualCount == 0).Label(
                    $"Expected UnreadCount=0 after ClearCount(), but got {actualCount} " +
                    $"(initial was {initialCount})");
            });
    }

    /// <summary>
    /// Property: After InitializeAsync with a mocked unread count N, applying decrements
    /// followed by ClearCount SHALL result in UnreadCount=0, and the decrements before
    /// ClearCount should never cause negative values.
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property DecrementsThenClear_NeverGoesNegativeAndClearResetsToZero()
    {
        // Generate initial unread count between 1 and 50,
        // then combine with a list of 1–10 decrement amounts (each 1–5).
        var gen = Gen.Choose(1, 50).SelectMany(initialCount =>
            Gen.Choose(1, 5).ListOf()
                .Select(list => list.Take(10).ToList())
                .Where(list => list.Count >= 1 && list.Count <= 10)
                .Select(decrements => (initialCount, decrements)));

        return Prop.ForAll(
            Arb.From(gen),
            ((int initialCount, List<int> decrements) input) =>
            {
                var (initialCount, decrements) = input;

                // Arrange: create context with mocked API returning the initial count
                var apiService = CreateMockedApiService(initialCount);
                var logger = NullLogger<NotificationContext>.Instance;
                var context = new NotificationContext(apiService, logger);

                // Act: initialize to load the count
                context.InitializeAsync().GetAwaiter().GetResult();

                // Track that count never goes negative during decrements
                var neverNegative = true;
                foreach (var amount in decrements)
                {
                    context.DecrementCount(amount);
                    if (context.UnreadCount < 0)
                        neverNegative = false;
                }

                // Now clear
                context.ClearCount();
                var finalCount = context.UnreadCount;

                return (neverNegative && finalCount == 0).Label(
                    $"NeverNegative={neverNegative}, FinalAfterClear={finalCount} " +
                    $"(initial={initialCount}, decrements=[{string.Join(",", decrements)}])");
            });
    }
}
