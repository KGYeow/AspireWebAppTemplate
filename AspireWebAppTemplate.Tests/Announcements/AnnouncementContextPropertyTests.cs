// Feature: announcement-banner-system, Property 13: Context dismissal removes from cached banner list
using System.Net;
using System.Text.Json;
using AspireWebAppTemplate.Core.Contracts.Announcements;
using AspireWebAppTemplate.Core.Domain.Enums;
using AspireWebAppTemplate.Web.Services;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

namespace AspireWebAppTemplate.Tests.Announcements;

/// <summary>
/// Property-based tests verifying that dismissing an announcement via the AnnouncementContext
/// removes it from the cached BannerAnnouncements list and fires the OnChange event.
/// </summary>
/// <remarks>
/// **Validates: Requirements 12.3**
/// </remarks>
public class AnnouncementContextPropertyTests
{
    /// <summary>
    /// Creates a mock HttpMessageHandler that returns different responses based on the request URL pattern.
    /// GET /api/announcements/active → returns the provided list of announcements as JSON.
    /// POST /api/announcements/{id}/dismiss → returns 200 OK with empty body.
    /// </summary>
    /// <param name="announcements">The list of announcements to return for the active query.</param>
    /// <returns>A configured mock HttpMessageHandler.</returns>
    private static Mock<HttpMessageHandler> CreateMockHandler(List<AnnouncementDto> announcements)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
            {
                var url = request.RequestUri?.PathAndQuery ?? string.Empty;

                if (url.Contains("/api/announcements/active"))
                {
                    var json = JsonSerializer.Serialize(announcements);
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                    };
                }

                if (url.Contains("/dismiss"))
                {
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(string.Empty)
                    };
                }

                return new HttpResponseMessage { StatusCode = HttpStatusCode.NotFound };
            });

        return mockHandler;
    }

    /// <summary>
    /// Generates a list of N active Banner-type announcements with unique IDs.
    /// </summary>
    /// <param name="count">The number of announcements to generate.</param>
    /// <returns>A list of Banner-type announcement DTOs.</returns>
    private static List<AnnouncementDto> GenerateBannerAnnouncements(int count)
    {
        var baseTime = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var announcements = new List<AnnouncementDto>();

        for (var i = 0; i < count; i++)
        {
            announcements.Add(new AnnouncementDto
            {
                Id = Guid.NewGuid(),
                Title = $"Banner Announcement {i + 1}",
                Message = $"<p>Content for announcement {i + 1}</p>",
                DisplayType = AnnouncementDisplayType.Banner,
                Severity = AnnouncementSeverity.Info,
                StartsAtUtc = null,
                ExpiresAtUtc = null,
                IsActive = true,
                NotifyUsers = false,
                Status = "Active",
                CreatedAtUtc = baseTime.AddHours(i),
                UpdatedAtUtc = baseTime.AddHours(i),
                CreatedByUserName = "admin"
            });
        }

        return announcements;
    }

    /// <summary>
    /// Property: For any AnnouncementContext loaded with N banner announcements (N from 1 to 4),
    /// dismissing one SHALL reduce the BannerAnnouncements count by one, the dismissed announcement
    /// SHALL not appear in BannerAnnouncements, and the OnChange event SHALL fire.
    /// **Validates: Requirements 12.3**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property ContextDismissal_RemovesFromCachedBannerList_And_FiresOnChange()
    {
        // Generate random N (1 to 4) and a random index within that range to dismiss.
        var inputGen = Gen.Choose(1, 4).SelectMany<int, (int count, int dismissIndex)>(count =>
            Gen.Choose(0, count - 1).Select(idx => (count, idx)));

        return Prop.ForAll(
            Arb.From(inputGen),
            ((int count, int dismissIndex) input) =>
            {
                // Arrange: create N banner announcements.
                var announcements = GenerateBannerAnnouncements(input.count);
                var targetId = announcements[input.dismissIndex].Id;

                var mockHandler = CreateMockHandler(announcements);
                var httpClient = new HttpClient(mockHandler.Object) { BaseAddress = new Uri("https://localhost") };
                var apiService = new ApiAnnouncementService(httpClient);
                var logger = NullLogger<AnnouncementContext>.Instance;
                var context = new AnnouncementContext(apiService, logger);

                // Act: initialize the context and verify initial state.
                context.InitializeAsync().GetAwaiter().GetResult();

                var initialCount = context.BannerAnnouncements.Count;
                var initialCountCorrect = initialCount == input.count;

                // Subscribe to OnChange to verify it fires.
                var onChangeFired = false;
                context.OnChange += () => onChangeFired = true;

                // Act: dismiss one announcement.
                context.DismissAsync(targetId).GetAwaiter().GetResult();

                // Assert: count reduced by one.
                var finalCount = context.BannerAnnouncements.Count;
                var countReduced = finalCount == input.count - 1;

                // Assert: dismissed announcement is not in the list.
                var dismissedNotPresent = !context.BannerAnnouncements.Any(a => a.Id == targetId);

                // Assert: OnChange event fired.
                var eventFired = onChangeFired;

                var result = initialCountCorrect && countReduced && dismissedNotPresent && eventFired;

                return result.Label(
                    $"Context dismissal failed. Count={input.count}, DismissIndex={input.dismissIndex}, " +
                    $"InitialCountCorrect={initialCountCorrect} (got {initialCount}), " +
                    $"CountReduced={countReduced} (got {finalCount}), " +
                    $"DismissedNotPresent={dismissedNotPresent}, " +
                    $"EventFired={eventFired}");
            });
    }
}
