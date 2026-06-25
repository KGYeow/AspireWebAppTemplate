using System.Security.Claims;
using AspireWebAppTemplate.ApiService.Abstractions;
using AspireWebAppTemplate.ApiService.Controllers;
using AspireWebAppTemplate.Core.Contracts;
using AspireWebAppTemplate.Core.Contracts.Notifications;
using AspireWebAppTemplate.Core.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace AspireWebAppTemplate.Tests.Notifications;

/// <summary>
/// Unit tests for <see cref="NotificationController"/> verifying HTTP-layer behavior only.
/// Mocks <see cref="INotificationService"/> to assert correct status codes, input validation,
/// and proper delegation of CurrentUserId to the service layer.
/// </summary>
public class NotificationControllerTests
{
    #region Setup

    private const string TestUserId = "test-user-id-123";

    private readonly Mock<INotificationService> _mockService;
    private readonly NotificationController _controller;

    /// <summary>
    /// Initializes test fixtures with a mocked INotificationService and a controller
    /// configured with an HttpContext containing a ClaimsPrincipal with a NameIdentifier claim.
    /// </summary>
    public NotificationControllerTests()
    {
        _mockService = new Mock<INotificationService>();
        _controller = new NotificationController(_mockService.Object);

        // Set up HttpContext with authenticated user claim
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, TestUserId)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    #endregion

    #region GetNotifications

    /// <summary>
    /// Verifies that GetNotifications returns 200 OK with a paginated result
    /// and passes CurrentUserId to the service.
    /// </summary>
    [Fact]
    public async Task GetNotifications_Returns200WithPaginatedResult()
    {
        // Arrange
        var expectedResult = new PagedResult<NotificationDto>
        {
            Items = [new NotificationDto { Id = Guid.NewGuid(), Title = "Test" }],
            TotalCount = 1,
            Page = 1,
            PageSize = 20
        };

        _mockService
            .Setup(s => s.GetNotificationsAsync(TestUserId, It.IsAny<NotificationQueryParams>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.GetNotifications();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var pagedResult = Assert.IsType<PagedResult<NotificationDto>>(okResult.Value);
        Assert.Equal(1, pagedResult.TotalCount);
        Assert.Single(pagedResult.Items);

        _mockService.Verify(
            s => s.GetNotificationsAsync(TestUserId, It.IsAny<NotificationQueryParams>()),
            Times.Once);
    }

    #endregion

    #region GetUnreadCount

    /// <summary>
    /// Verifies that GetUnreadCount returns 200 OK with the integer count
    /// and passes CurrentUserId to the service.
    /// </summary>
    [Fact]
    public async Task GetUnreadCount_Returns200WithCount()
    {
        // Arrange
        _mockService
            .Setup(s => s.GetUnreadCountAsync(TestUserId))
            .ReturnsAsync(7);

        // Act
        var result = await _controller.GetUnreadCount();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(7, okResult.Value);

        _mockService.Verify(s => s.GetUnreadCountAsync(TestUserId), Times.Once);
    }

    #endregion

    #region GetRecent

    /// <summary>
    /// Verifies that GetRecent returns 200 OK with a list of notifications
    /// and passes CurrentUserId to the service.
    /// </summary>
    [Fact]
    public async Task GetRecent_Returns200WithList()
    {
        // Arrange
        var recentList = new List<NotificationDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Recent 1" },
            new() { Id = Guid.NewGuid(), Title = "Recent 2" }
        };

        _mockService
            .Setup(s => s.GetRecentAsync(TestUserId, 5))
            .ReturnsAsync(recentList);

        // Act
        var result = await _controller.GetRecent();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsType<List<NotificationDto>>(okResult.Value);
        Assert.Equal(2, list.Count);

        _mockService.Verify(s => s.GetRecentAsync(TestUserId, 5), Times.Once);
    }

    #endregion

    #region MarkAsRead

    /// <summary>
    /// Verifies that MarkAsRead returns 200 OK when the service returns true
    /// (notification found and belongs to user).
    /// </summary>
    [Fact]
    public async Task MarkAsRead_Returns200_WhenServiceReturnsTrue()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        _mockService
            .Setup(s => s.MarkAsReadAsync(TestUserId, notificationId))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.MarkAsRead(notificationId);

        // Assert
        Assert.IsType<OkResult>(result);
        _mockService.Verify(s => s.MarkAsReadAsync(TestUserId, notificationId), Times.Once);
    }

    /// <summary>
    /// Verifies that MarkAsRead returns 404 Not Found when the service returns false
    /// (notification not found or not owned by user).
    /// </summary>
    [Fact]
    public async Task MarkAsRead_Returns404_WhenServiceReturnsFalse()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        _mockService
            .Setup(s => s.MarkAsReadAsync(TestUserId, notificationId))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.MarkAsRead(notificationId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
        _mockService.Verify(s => s.MarkAsReadAsync(TestUserId, notificationId), Times.Once);
    }

    #endregion

    #region MarkAllAsRead

    /// <summary>
    /// Verifies that MarkAllAsRead returns 200 OK with the count of updated notifications
    /// and passes CurrentUserId to the service.
    /// </summary>
    [Fact]
    public async Task MarkAllAsRead_Returns200WithCount()
    {
        // Arrange
        _mockService
            .Setup(s => s.MarkAllAsReadAsync(TestUserId))
            .ReturnsAsync(5);

        // Act
        var result = await _controller.MarkAllAsRead();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(5, okResult.Value);

        _mockService.Verify(s => s.MarkAllAsReadAsync(TestUserId), Times.Once);
    }

    #endregion

    #region BulkDismiss

    /// <summary>
    /// Verifies that BulkDismiss returns 200 OK with deleted count when ≤100 IDs are provided.
    /// </summary>
    [Fact]
    public async Task BulkDismiss_Returns200WithDeletedCount_WhenAtMost100Ids()
    {
        // Arrange
        var ids = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToList();
        var request = new BulkDismissRequest { NotificationIds = ids };

        _mockService
            .Setup(s => s.BulkDismissAsync(TestUserId, ids))
            .ReturnsAsync(45);

        // Act
        var result = await _controller.BulkDismiss(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(45, okResult.Value);

        _mockService.Verify(s => s.BulkDismissAsync(TestUserId, ids), Times.Once);
    }

    /// <summary>
    /// Verifies that BulkDismiss returns 400 Bad Request when >100 IDs are provided,
    /// WITHOUT calling the service at all.
    /// </summary>
    [Fact]
    public async Task BulkDismiss_Returns400_WhenMoreThan100Ids_WithoutCallingService()
    {
        // Arrange
        var ids = Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToList();
        var request = new BulkDismissRequest { NotificationIds = ids };

        // Act
        var result = await _controller.BulkDismiss(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("100", badRequestResult.Value?.ToString());

        // Verify the service was NEVER called
        _mockService.Verify(
            s => s.BulkDismissAsync(It.IsAny<string>(), It.IsAny<List<Guid>>()),
            Times.Never);
    }

    #endregion

    #region GetPreferences

    /// <summary>
    /// Verifies that GetPreferences returns 200 OK with a list of preferences
    /// and passes CurrentUserId to the service.
    /// </summary>
    [Fact]
    public async Task GetPreferences_Returns200WithPreferencesList()
    {
        // Arrange
        var preferences = new List<NotificationPreferenceDto>
        {
            new() { Category = NotificationCategory.Account, InAppEnabled = true, EmailEnabled = true },
            new() { Category = NotificationCategory.Activity, InAppEnabled = false, EmailEnabled = true }
        };

        _mockService
            .Setup(s => s.GetPreferencesAsync(TestUserId))
            .ReturnsAsync(preferences);

        // Act
        var result = await _controller.GetPreferences();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsType<List<NotificationPreferenceDto>>(okResult.Value);
        Assert.Equal(2, list.Count);

        _mockService.Verify(s => s.GetPreferencesAsync(TestUserId), Times.Once);
    }

    #endregion

    #region UpdatePreference

    /// <summary>
    /// Verifies that UpdatePreference returns 200 OK on successful update
    /// and passes CurrentUserId to the service.
    /// </summary>
    [Fact]
    public async Task UpdatePreference_Returns200_OnSuccess()
    {
        // Arrange
        var request = new UpdateNotificationPreferenceRequest
        {
            Category = NotificationCategory.Account,
            InAppEnabled = true,
            EmailEnabled = false
        };

        _mockService
            .Setup(s => s.UpdatePreferenceAsync(TestUserId, request))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.UpdatePreference(request);

        // Assert
        Assert.IsType<OkResult>(result);
        _mockService.Verify(s => s.UpdatePreferenceAsync(TestUserId, request), Times.Once);
    }

    /// <summary>
    /// Verifies that UpdatePreference returns 400 Bad Request when the service
    /// throws an ArgumentException.
    /// </summary>
    [Fact]
    public async Task UpdatePreference_Returns400_OnArgumentException()
    {
        // Arrange
        var request = new UpdateNotificationPreferenceRequest
        {
            Category = NotificationCategory.System,
            InAppEnabled = true,
            EmailEnabled = true
        };

        _mockService
            .Setup(s => s.UpdatePreferenceAsync(TestUserId, request))
            .ThrowsAsync(new ArgumentException("Invalid category value."));

        // Act
        var result = await _controller.UpdatePreference(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid category value.", badRequestResult.Value);
    }

    /// <summary>
    /// Verifies that UpdatePreference returns 404 Not Found when the service
    /// throws a KeyNotFoundException.
    /// </summary>
    [Fact]
    public async Task UpdatePreference_Returns404_OnKeyNotFoundException()
    {
        // Arrange
        var request = new UpdateNotificationPreferenceRequest
        {
            Category = NotificationCategory.Account,
            InAppEnabled = true,
            EmailEnabled = true
        };

        _mockService
            .Setup(s => s.UpdatePreferenceAsync(TestUserId, request))
            .ThrowsAsync(new KeyNotFoundException("Preference not found."));

        // Act
        var result = await _controller.UpdatePreference(request);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Preference not found.", notFoundResult.Value);
    }

    #endregion
}
