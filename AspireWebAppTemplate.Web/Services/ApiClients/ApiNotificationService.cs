using System.Net.Http.Json;
using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Core.Contracts;
using AspireWebAppTemplate.Core.Contracts.Notifications;

namespace AspireWebAppTemplate.Web.Services;

/// <summary>
/// Typed HttpClient service for notification API operations.
/// Uses Aspire service discovery and <see cref="UserIdentityDelegatingHandler"/> for auth propagation.
/// Wraps calls to the ApiService's NotificationController endpoints.
/// </summary>
public class ApiNotificationService(HttpClient http)
{
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
        var url = $"/api/notifications?page={queryParams.Page}&pageSize={queryParams.PageSize}";
        if (queryParams.Category.HasValue)
            url += $"&category={queryParams.Category.Value}";
        if (queryParams.IsRead.HasValue)
            url += $"&isRead={queryParams.IsRead.Value}";

        var response = await http.GetAsync(url);
        if (response.IsSuccessStatusCode)
            return ApiResult<PagedResult<NotificationDto>>.Success(await response.Content.ReadFromJsonAsync<PagedResult<NotificationDto>>()!);
        return ApiResult<PagedResult<NotificationDto>>.Failure(await response.Content.ReadAsStringAsync());
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
        var response = await http.GetAsync("/api/notifications/unread-count");
        if (response.IsSuccessStatusCode)
            return ApiResult<int>.Success(await response.Content.ReadFromJsonAsync<int>());
        return ApiResult<int>.Failure(await response.Content.ReadAsStringAsync());
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
        var response = await http.GetAsync("/api/notifications/recent");
        if (response.IsSuccessStatusCode)
            return ApiResult<List<NotificationDto>>.Success(await response.Content.ReadFromJsonAsync<List<NotificationDto>>() ?? []);
        return ApiResult<List<NotificationDto>>.Failure(await response.Content.ReadAsStringAsync());
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
        var response = await http.PutAsync($"/api/notifications/{notificationId}/read", null);
        if (response.IsSuccessStatusCode) return ApiResult.Success();
        return ApiResult.Failure(await response.Content.ReadAsStringAsync());
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
        var response = await http.PutAsync("/api/notifications/read-all", null);
        if (response.IsSuccessStatusCode)
            return ApiResult<int>.Success(await response.Content.ReadFromJsonAsync<int>());
        return ApiResult<int>.Failure(await response.Content.ReadAsStringAsync());
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
        var response = await http.PostAsJsonAsync("/api/notifications/dismiss", request);
        if (response.IsSuccessStatusCode)
            return ApiResult<int>.Success(await response.Content.ReadFromJsonAsync<int>());
        return ApiResult<int>.Failure(await response.Content.ReadAsStringAsync());
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
        var response = await http.GetAsync("/api/notifications/preferences");
        if (response.IsSuccessStatusCode)
            return ApiResult<List<NotificationPreferenceDto>>.Success(await response.Content.ReadFromJsonAsync<List<NotificationPreferenceDto>>() ?? []);
        return ApiResult<List<NotificationPreferenceDto>>.Failure(await response.Content.ReadAsStringAsync());
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
        var response = await http.PutAsJsonAsync("/api/notifications/preferences", request);
        if (response.IsSuccessStatusCode) return ApiResult.Success();
        return ApiResult.Failure(await response.Content.ReadAsStringAsync());
    }

    #endregion
}
