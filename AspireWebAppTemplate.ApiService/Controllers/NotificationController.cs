using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Application.Contracts.Notifications;
using AspireWebAppTemplate.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AspireWebAppTemplate.ApiService.Controllers;

/// <summary>
/// Provides notification query, mutation, and preference management endpoints.
/// This controller is intentionally thin — it handles HTTP concerns only (request parsing,
/// user identity extraction, status code mapping) and delegates all business logic to
/// <see cref="INotificationService"/>.
/// </summary>
/// <remarks>
/// <para>
/// Exception-to-HTTP-status mapping:
/// <list type="bullet">
///   <item><see cref="KeyNotFoundException"/> → 404 Not Found</item>
///   <item><see cref="InvalidOperationException"/> → 400 Bad Request</item>
///   <item><see cref="ArgumentException"/> → 400 Bad Request</item>
/// </list>
/// </para>
/// </remarks>
[Route("api/notifications")]
[Authorize]
public class NotificationController : BaseController
{
    #region Constructor

    private readonly INotificationService _notificationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationController"/> class.
    /// </summary>
    /// <param name="notificationService">The notification service for managing notification operations.</param>
    public NotificationController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    #endregion

    #region Endpoints

    /// <summary>
    /// Retrieves a paginated list of notifications for the authenticated user,
    /// with optional category and read-status filters.
    /// </summary>
    /// <param name="page">The one-based page index. Defaults to 1.</param>
    /// <param name="pageSize">The maximum number of items per page. Defaults to 20, maximum 100.</param>
    /// <param name="category">Optional filter by notification category.</param>
    /// <param name="isRead">Optional filter by read status.</param>
    /// <returns>A paginated list of notifications ordered by creation date descending.</returns>
    /// <response code="200">Returns the paginated notification list.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<NotificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<NotificationDto>>> GetNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] NotificationCategory? category = null,
        [FromQuery] bool? isRead = null)
    {
        var queryParams = new NotificationQueryParams
        {
            Page = page,
            PageSize = pageSize,
            Category = category,
            IsRead = isRead
        };

        var result = await _notificationService.GetNotificationsAsync(CurrentUserId!, queryParams);
        return Ok(result);
    }

    /// <summary>
    /// Returns the count of unread notifications for the authenticated user.
    /// </summary>
    /// <returns>The integer count of unread notifications.</returns>
    /// <response code="200">Returns the unread notification count.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<int>> GetUnreadCount()
    {
        var count = await _notificationService.GetUnreadCountAsync(CurrentUserId!);
        return Ok(count);
    }

    /// <summary>
    /// Returns the most recent notifications for the bell dropdown preview.
    /// Returns up to 5 notifications ordered by creation date descending.
    /// </summary>
    /// <returns>A list of the most recent notifications.</returns>
    /// <response code="200">Returns the recent notifications list.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(List<NotificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<NotificationDto>>> GetRecent()
    {
        var recent = await _notificationService.GetRecentAsync(CurrentUserId!, count: 10);
        return Ok(recent);
    }

    /// <summary>
    /// Marks a single notification as read for the authenticated user.
    /// Idempotent: succeeds even if the notification is already marked as read.
    /// </summary>
    /// <param name="id">The unique identifier of the notification to mark as read.</param>
    /// <returns>200 OK if successful, 404 if the notification was not found or does not belong to the user.</returns>
    /// <response code="200">The notification was marked as read successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">The notification was not found or does not belong to the user.</response>
    [HttpPut("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var found = await _notificationService.MarkAsReadAsync(CurrentUserId!, id);

        if (!found)
            return NotFound();

        return Ok();
    }

    /// <summary>
    /// Marks a single notification as unread for the authenticated user.
    /// Idempotent: succeeds even if already unread.
    /// </summary>
    /// <param name="id">The unique identifier of the notification to mark as unread.</param>
    /// <returns>200 OK if successful, 404 if the notification was not found or does not belong to the user.</returns>
    /// <response code="200">The notification was marked as unread successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">The notification was not found or does not belong to the user.</response>
    [HttpPut("{id:guid}/unread")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsUnread(Guid id)
    {
        var found = await _notificationService.MarkAsUnreadAsync(CurrentUserId!, id);

        if (!found)
            return NotFound();

        return Ok();
    }

    /// <summary>
    /// Marks all unread notifications as read for the authenticated user.
    /// Returns the count of notifications that were actually updated.
    /// </summary>
    /// <returns>The number of notifications that were marked as read.</returns>
    /// <response code="200">Returns the count of updated notifications.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPut("read-all")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<int>> MarkAllAsRead()
    {
        var updatedCount = await _notificationService.MarkAllAsReadAsync(CurrentUserId!);
        return Ok(updatedCount);
    }

    /// <summary>
    /// Dismisses (deletes) multiple notifications belonging to the authenticated user.
    /// IDs not owned by the user or non-existent are silently ignored.
    /// A maximum of 100 notification IDs can be dismissed per request.
    /// </summary>
    /// <param name="request">The request containing the list of notification IDs to dismiss.</param>
    /// <returns>The count of notifications that were actually deleted.</returns>
    /// <response code="200">Returns the count of dismissed notifications.</response>
    /// <response code="400">The request contains more than 100 notification IDs.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPost("dismiss")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> BulkDismiss([FromBody] BulkDismissRequest request)
    {
        if (request.NotificationIds.Count > 100)
            return BadRequest("A maximum of 100 notification IDs can be dismissed per request.");

        var deletedCount = await _notificationService.BulkDismissAsync(CurrentUserId!, request.NotificationIds);
        return Ok(deletedCount);
    }

    /// <summary>
    /// Retrieves notification delivery preferences for all categories for the authenticated user.
    /// Categories without an explicit preference record are returned with default values
    /// (InAppEnabled=true, EmailEnabled=true).
    /// </summary>
    /// <returns>A list of notification preferences, one per category.</returns>
    /// <response code="200">Returns the user's notification preferences.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("preferences")]
    [ProducesResponseType(typeof(List<NotificationPreferenceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<NotificationPreferenceDto>>> GetPreferences()
    {
        var preferences = await _notificationService.GetPreferencesAsync(CurrentUserId!);
        return Ok(preferences);
    }

    /// <summary>
    /// Updates a single notification delivery preference for the authenticated user.
    /// Creates a new preference record if none exists for the user-category pair,
    /// or updates the existing record.
    /// </summary>
    /// <param name="request">The preference update request containing category and toggle states.</param>
    /// <returns>200 OK on success.</returns>
    /// <response code="200">The preference was updated successfully.</response>
    /// <response code="400">The request is invalid.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPut("preferences")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdatePreference([FromBody] UpdateNotificationPreferenceRequest request)
    {
        try
        {
            await _notificationService.UpdatePreferenceAsync(CurrentUserId!, request);
            return Ok();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    #endregion
}
