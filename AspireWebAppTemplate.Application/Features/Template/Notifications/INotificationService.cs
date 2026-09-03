using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Application.Features.Template.Notifications;

namespace AspireWebAppTemplate.Application.Features.Template.Notifications;

/// <summary>
/// Defines the contract for notification business logic including creation, retrieval,
/// status management, and preference management. All database access for notifications
/// is encapsulated here — controllers delegate to this service without touching DbContext.
/// </summary>
/// <remarks>
/// Implementations should be registered as scoped services to align with the per-request
/// <c>DbContext</c> lifetime. The <see cref="CreateNotificationAsync"/> method is also
/// called as a cross-cutting concern by other services (UserController, RoleController, etc.)
/// to notify users of significant events.
/// </remarks>
public interface INotificationService
{
    #region Creation

    /// <summary>
    /// Creates a new notification for the specified user, respecting their delivery preferences.
    /// If the user does not exist or has InAppEnabled=false for the category, no entity is created.
    /// Failures are logged but never propagated to the caller, ensuring notification creation
    /// does not disrupt the primary user operation.
    /// </summary>
    /// <param name="request">
    /// A <see cref="CreateNotificationRequest"/> containing the target user ID, category,
    /// title, and message for the notification.
    /// </param>
    /// <returns>A task representing the asynchronous create operation.</returns>
    /// <remarks>
    /// This method is designed to be safe for fire-and-forget usage. Any exceptions
    /// (user not found, database errors, preference checks) are caught internally and
    /// logged at Error level via <c>ILogger</c>. The caller is never interrupted.
    /// </remarks>
    Task CreateNotificationAsync(CreateNotificationRequest request);

    #endregion

    #region Retrieval

    /// <summary>
    /// Retrieves a paginated list of notifications for the specified user,
    /// ordered by CreatedAtUtc descending, with optional category and read-status filters.
    /// </summary>
    /// <param name="userId">The unique identifier of the user whose notifications are being queried.</param>
    /// <param name="queryParams">
    /// A <see cref="NotificationQueryParams"/> containing pagination (page, pageSize)
    /// and optional filter criteria (category, isRead).
    /// </param>
    /// <returns>
    /// A task that resolves to a <see cref="PagedResult{T}"/> of <see cref="NotificationDto"/>
    /// containing the matching notifications and total count metadata.
    /// </returns>
    /// <remarks>
    /// Results are always ordered by <c>CreatedAtUtc</c> descending (newest first).
    /// PageSize is clamped to a maximum of 100.
    /// </remarks>
    Task<PagedResult<NotificationDto>> GetNotificationsAsync(string userId, NotificationQueryParams queryParams);

    /// <summary>
    /// Returns the count of unread notifications (IsRead=false) for the specified user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user whose unread count is being queried.</param>
    /// <returns>
    /// A task that resolves to the number of unread notifications for the user.
    /// Returns 0 if the user has no notifications.
    /// </returns>
    Task<int> GetUnreadCountAsync(string userId);

    /// <summary>
    /// Returns the most recent notifications for the bell dropdown preview.
    /// </summary>
    /// <param name="userId">The unique identifier of the user whose recent notifications are being queried.</param>
    /// <param name="count">
    /// The maximum number of recent notifications to return. Defaults to 5.
    /// </param>
    /// <returns>
    /// A task that resolves to a list of the most recent <see cref="NotificationDto"/> records,
    /// ordered by CreatedAtUtc descending.
    /// </returns>
    /// <remarks>
    /// Returns both read and unread notifications. The list may contain fewer items than
    /// <paramref name="count"/> if the user has fewer total notifications.
    /// </remarks>
    Task<List<NotificationDto>> GetRecentAsync(string userId, int count = 5);

    #endregion

    #region Status Management

    /// <summary>
    /// Marks a single notification as read. Sets IsRead=true and ReadAtUtc to the current UTC time.
    /// Returns true if the notification was found and belongs to the user; false otherwise.
    /// </summary>
    /// <param name="userId">The unique identifier of the authenticated user performing the action.</param>
    /// <param name="notificationId">The unique identifier of the notification to mark as read.</param>
    /// <returns>
    /// A task that resolves to <c>true</c> if the notification was found and belongs to the user;
    /// <c>false</c> if the notification does not exist or belongs to a different user.
    /// </returns>
    /// <remarks>
    /// Idempotent: if the notification is already marked as read, the method completes
    /// successfully without modifying the <c>ReadAtUtc</c> timestamp.
    /// </remarks>
    Task<bool> MarkAsReadAsync(string userId, Guid notificationId);

    /// <summary>
    /// Marks a single notification as unread for the specified user.
    /// Sets IsRead to false and clears ReadAtUtc.
    /// Idempotent: succeeds even if already unread.
    /// </summary>
    /// <param name="userId">The user who owns the notification.</param>
    /// <param name="notificationId">The notification to mark as unread.</param>
    /// <returns>True if the notification was found; false if not found or doesn't belong to the user.</returns>
    Task<bool> MarkAsUnreadAsync(string userId, Guid notificationId);

    /// <summary>
    /// Marks all unread notifications for the specified user as read.
    /// Sets IsRead=true and ReadAtUtc to the current UTC time on all matching records.
    /// </summary>
    /// <param name="userId">The unique identifier of the authenticated user performing the action.</param>
    /// <returns>
    /// A task that resolves to the count of notifications that were actually updated
    /// (i.e., those that were previously unread).
    /// </returns>
    /// <remarks>
    /// Returns 0 if the user has no unread notifications. Already-read notifications
    /// are not modified.
    /// </remarks>
    Task<int> MarkAllAsReadAsync(string userId);

    #endregion

    #region Bulk Operations

    /// <summary>
    /// Deletes the specified notifications that belong to the user.
    /// IDs that do not exist or do not belong to the user are silently ignored.
    /// </summary>
    /// <param name="userId">The unique identifier of the authenticated user performing the action.</param>
    /// <param name="notificationIds">
    /// The list of notification IDs to dismiss (delete). Maximum 100 IDs per request
    /// (enforced at the controller level).
    /// </param>
    /// <returns>
    /// A task that resolves to the count of notifications that were actually deleted
    /// (only those existing and belonging to the user).
    /// </returns>
    /// <remarks>
    /// IDs belonging to other users or referencing non-existent notifications are silently
    /// ignored — no exception is thrown and no error is reported for those IDs.
    /// </remarks>
    Task<int> BulkDismissAsync(string userId, List<Guid> notificationIds);

    #endregion

    #region Preferences

    /// <summary>
    /// Retrieves notification preferences for all categories for the specified user.
    /// Categories without an explicit preference record are returned with defaults
    /// (InAppEnabled=true, EmailEnabled=true).
    /// </summary>
    /// <param name="userId">The unique identifier of the user whose preferences are being queried.</param>
    /// <returns>
    /// A task that resolves to a list of <see cref="NotificationPreferenceDto"/> objects,
    /// one per <see cref="Domain.Enums.NotificationCategory"/> value. Categories without
    /// a stored preference record use default values (both channels enabled).
    /// </returns>
    Task<List<NotificationPreferenceDto>> GetPreferencesAsync(string userId);

    /// <summary>
    /// Creates or updates the notification preference for the specified user-category pair.
    /// If no preference record exists, one is created; otherwise the existing record is updated.
    /// </summary>
    /// <param name="userId">The unique identifier of the user whose preference is being updated.</param>
    /// <param name="request">
    /// An <see cref="UpdateNotificationPreferenceRequest"/> containing the category and
    /// the desired InAppEnabled/EmailEnabled toggle states.
    /// </param>
    /// <returns>A task representing the asynchronous update operation.</returns>
    /// <remarks>
    /// Uses an upsert pattern: inserts a new preference record if none exists for the
    /// user-category pair, or updates the existing record's toggle values.
    /// </remarks>
    Task UpdatePreferenceAsync(string userId, UpdateNotificationPreferenceRequest request);

    #endregion
}
