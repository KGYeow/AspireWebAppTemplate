using System.Net.Http.Json;
using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Application.Features.Notifications;
using AspireWebAppTemplate.Web.Extensions;
using AspireWebAppTemplate.Web.Utilities;

namespace AspireWebAppTemplate.Web.Services;

/// <summary>
/// Typed HttpClient service for notification API operations.
/// Uses Aspire service discovery and <see cref="UserIdentityDelegatingHandler"/> for auth propagation.
/// Wraps calls to the ApiService's NotificationController endpoints.
/// </summary>
public class ApiNotificationService
{
    #region Constructor

    /// <summary>
    /// The underlying HttpClient configured with the ApiService base address.
    /// </summary>
    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of <see cref="ApiNotificationService"/> with the configured HttpClient.
    /// </summary>
    /// <param name="http">The HttpClient instance configured via Aspire service discovery.</param>
    public ApiNotificationService(HttpClient http)
    {
        _http = http;
    }

    #endregion

    #region Query

    /// <summary>
    /// Retrieves a paginated list of notifications for the authenticated user,
    /// with optional category and read-status filters.
    /// Calls GET /api/notifications.
    /// </summary>
    /// <param name="queryParams">The pagination and filter parameters.</param>
    /// <returns>
    /// An <see cref="ApiResult{T}"/> containing the paged notification list on success,
    /// or an error message on failure.
    /// </returns>
    public async Task<ApiResult<PagedResult<NotificationDto>>> GetNotificationsAsync(NotificationQueryParams queryParams)
    {
        // page and pageSize are always emitted; category and isRead only when the nullable has a value.
        var url = QueryStringBuilder.Build("/api/notifications", qs =>
        {
            qs.Add("page", queryParams.Page);
            qs.Add("pageSize", queryParams.PageSize);
            qs.AddIfHasValue("category", queryParams.Category);
            qs.AddIfHasValue("isRead", queryParams.IsRead);
        });

        var response = await _http.GetAsync(url);
        return await response.ToApiResultAsync<PagedResult<NotificationDto>>();
    }

    /// <summary>
    /// Returns the total count of unread notifications for the authenticated user.
    /// Calls GET /api/notifications/unread-count.
    /// </summary>
    /// <returns>
    /// An <see cref="ApiResult{T}"/> containing the unread count on success,
    /// or an error message on failure.
    /// </returns>
    public async Task<ApiResult<int>> GetUnreadCountAsync()
    {
        var response = await _http.GetAsync("/api/notifications/unread-count");
        return await response.ToApiResultAsync<int>();
    }

    /// <summary>
    /// Returns the most recent notifications for the bell dropdown preview.
    /// Calls GET /api/notifications/recent.
    /// </summary>
    /// <returns>
    /// An <see cref="ApiResult{T}"/> containing a list of recent notifications on success,
    /// or an error message on failure.
    /// </returns>
    public async Task<ApiResult<List<NotificationDto>>> GetRecentAsync()
    {
        var response = await _http.GetAsync("/api/notifications/recent");
        return await response.ToApiResultAsync<List<NotificationDto>>(defaultValue: []);
    }

    #endregion

    #region Mutations

    /// <summary>
    /// Marks a single notification as read for the authenticated user.
    /// Calls PUT /api/notifications/{id}/read.
    /// </summary>
    /// <param name="notificationId">The unique identifier of the notification to mark as read.</param>
    /// <returns>
    /// An <see cref="ApiResult"/> indicating success or failure with an error message.
    /// </returns>
    public async Task<ApiResult> MarkAsReadAsync(Guid notificationId)
    {
        var response = await _http.PutAsync($"/api/notifications/{notificationId}/read", null);
        return await response.ToApiResultAsync();
    }

    /// <summary>
    /// Marks a single notification as unread for the authenticated user.
    /// Calls PUT /api/notifications/{id}/unread.
    /// </summary>
    /// <param name="notificationId">The unique identifier of the notification to mark as unread.</param>
    /// <returns>
    /// An <see cref="ApiResult"/> indicating success or failure with an error message.
    /// </returns>
    public async Task<ApiResult> MarkAsUnreadAsync(Guid notificationId)
    {
        var response = await _http.PutAsync($"/api/notifications/{notificationId}/unread", null);
        return await response.ToApiResultAsync();
    }

    /// <summary>
    /// Marks all unread notifications as read for the authenticated user.
    /// Calls PUT /api/notifications/read-all.
    /// </summary>
    /// <returns>
    /// An <see cref="ApiResult{T}"/> containing the count of notifications updated on success,
    /// or an error message on failure.
    /// </returns>
    public async Task<ApiResult<int>> MarkAllAsReadAsync()
    {
        var response = await _http.PutAsync("/api/notifications/read-all", null);
        return await response.ToApiResultAsync<int>();
    }

    /// <summary>
    /// Dismisses (deletes) multiple notifications belonging to the authenticated user.
    /// Calls POST /api/notifications/dismiss.
    /// </summary>
    /// <param name="request">The request containing the list of notification IDs to dismiss.</param>
    /// <returns>
    /// An <see cref="ApiResult{T}"/> containing the count of notifications deleted on success,
    /// or an error message on failure.
    /// </returns>
    public async Task<ApiResult<int>> BulkDismissAsync(BulkDismissRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/notifications/dismiss", request);
        return await response.ToApiResultAsync<int>();
    }

    #endregion

    #region Preferences

    /// <summary>
    /// Retrieves notification preferences for all categories for the authenticated user.
    /// Calls GET /api/notifications/preferences.
    /// </summary>
    /// <returns>
    /// An <see cref="ApiResult{T}"/> containing the list of preferences on success,
    /// or an error message on failure.
    /// </returns>
    public async Task<ApiResult<List<NotificationPreferenceDto>>> GetPreferencesAsync()
    {
        var response = await _http.GetAsync("/api/notifications/preferences");
        return await response.ToApiResultAsync<List<NotificationPreferenceDto>>(defaultValue: []);
    }

    /// <summary>
    /// Updates a single notification delivery preference for the authenticated user.
    /// Calls PUT /api/notifications/preferences.
    /// </summary>
    /// <param name="request">The preference update request containing category and channel toggles.</param>
    /// <returns>
    /// An <see cref="ApiResult"/> indicating success or failure with an error message.
    /// </returns>
    public async Task<ApiResult> UpdatePreferenceAsync(UpdateNotificationPreferenceRequest request)
    {
        var response = await _http.PutAsJsonAsync("/api/notifications/preferences", request);
        return await response.ToApiResultAsync();
    }

    #endregion
}
