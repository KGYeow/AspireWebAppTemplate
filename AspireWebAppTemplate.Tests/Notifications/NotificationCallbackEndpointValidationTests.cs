using AspireWebAppTemplate.Application.Features.Template.Notifications;
using AspireWebAppTemplate.Web.Endpoints;
using AspireWebAppTemplate.Web.Hubs;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace AspireWebAppTemplate.Tests.Notifications;

/// <summary>
/// Unit tests for <see cref="NotificationCallbackEndpoint"/> input validation.
/// Verifies that requests with <c>Guid.Empty</c> NotificationId are rejected with 400.
/// </summary>
public class NotificationCallbackEndpointValidationTests
{
    #region Setup

    private readonly Mock<IHubContext<NotificationHub>> _mockHubContext;

    /// <summary>
    /// Initializes test fixtures with a mocked IHubContext that will not be reached
    /// due to early validation failure.
    /// </summary>
    public NotificationCallbackEndpointValidationTests()
    {
        _mockHubContext = new Mock<IHubContext<NotificationHub>>();
    }

    #endregion

    #region NotificationId Validation

    /// <summary>
    /// Verifies that a request with <c>Guid.Empty</c> NotificationId returns 400 Bad Request
    /// with the message "NotificationId is required."
    /// </summary>
    /// <remarks>Validates: Requirements 1.3</remarks>
    [Fact]
    public async Task HandlePush_ReturnssBadRequest_WhenNotificationIdIsGuidEmpty()
    {
        // Arrange
        var request = new NotificationPushRequest
        {
            UserId = "valid-user-id",
            Title = "Valid Title",
            Category = "System",
            UnreadCount = 1,
            Message = "Some message",
            NotificationId = Guid.Empty
        };

        // Act
        var result = await NotificationCallbackEndpoint.HandlePush(request, _mockHubContext.Object);

        // Assert
        var badRequest = Assert.IsType<BadRequest<string>>(result);
        Assert.Equal("NotificationId is required.", badRequest.Value);
    }

    #endregion
}
