using System.Net.Http.Json;
using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Application.Contracts.Announcements;
using AspireWebAppTemplate.Domain.Enums;

namespace AspireWebAppTemplate.Web.Services;

/// <summary>
/// Typed HttpClient service for announcement API operations.
/// Uses Aspire service discovery and <see cref="UserIdentityDelegatingHandler"/> for auth propagation.
/// Wraps calls to the ApiService's AnnouncementController endpoints.
/// </summary>
public class ApiAnnouncementService
{
    #region Constructor

    /// <summary>
    /// The underlying HttpClient configured with the ApiService base address.
    /// </summary>
    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of <see cref="ApiAnnouncementService"/> with the configured HttpClient.
    /// </summary>
    /// <param name="http">The HttpClient instance configured via Aspire service discovery.</param>
    public ApiAnnouncementService(HttpClient http)
    {
        _http = http;
    }

    #endregion

    #region User Queries

    /// <summary>
    /// Retrieves active, non-dismissed announcements for the current user.
    /// Used by the AnnouncementContext for banner and dashboard display.
    /// Calls GET /api/announcements/active.
    /// </summary>
    /// <returns>
    /// An <see cref="ApiResult{T}"/> containing the list of active announcements for the user on success,
    /// or an error message on failure.
    /// </returns>
    public async Task<ApiResult<List<AnnouncementDto>>> GetActiveForUserAsync()
    {
        var response = await _http.GetAsync("/api/announcements/active");
        if (response.IsSuccessStatusCode)
            return ApiResult<List<AnnouncementDto>>.Success(await response.Content.ReadFromJsonAsync<List<AnnouncementDto>>() ?? []);
        return ApiResult<List<AnnouncementDto>>.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Retrieves active and recently expired announcements for the list page with pagination.
    /// Calls GET /api/announcements/list?page={page}&amp;pageSize={pageSize}&amp;severity={severity}.
    /// </summary>
    /// <param name="queryParams">Pagination and filter parameters.</param>
    /// <returns>
    /// An <see cref="ApiResult{T}"/> containing the paginated announcements on success,
    /// or an error message on failure.
    /// </returns>
    public async Task<ApiResult<PagedResult<AnnouncementDto>>> GetForListPageAsync(AnnouncementQueryParams queryParams)
    {
        var url = $"/api/announcements/list?page={queryParams.Page}&pageSize={queryParams.PageSize}";
        if (queryParams.Severity.HasValue)
            url += $"&severity={queryParams.Severity.Value}";

        var response = await _http.GetAsync(url);
        if (response.IsSuccessStatusCode)
            return ApiResult<PagedResult<AnnouncementDto>>.Success(await response.Content.ReadFromJsonAsync<PagedResult<AnnouncementDto>>() ?? new());
        return ApiResult<PagedResult<AnnouncementDto>>.Failure(await response.Content.ReadAsStringAsync());
    }

    #endregion

    #region Admin Queries

    /// <summary>
    /// Retrieves all announcements for admin management.
    /// Calls GET /api/announcements.
    /// </summary>
    /// <returns>
    /// An <see cref="ApiResult{T}"/> containing all announcements on success,
    /// or an error message on failure.
    /// </returns>
    public async Task<ApiResult<List<AnnouncementDto>>> GetAllAsync()
    {
        var response = await _http.GetAsync("/api/announcements");
        if (response.IsSuccessStatusCode)
            return ApiResult<List<AnnouncementDto>>.Success(await response.Content.ReadFromJsonAsync<List<AnnouncementDto>>() ?? []);
        return ApiResult<List<AnnouncementDto>>.Failure(await response.Content.ReadAsStringAsync());
    }

    #endregion

    #region Admin Mutations

    /// <summary>
    /// Creates a new announcement with the specified properties.
    /// Calls POST /api/announcements.
    /// </summary>
    /// <param name="request">The create announcement request containing all announcement fields.</param>
    /// <returns>
    /// An <see cref="ApiResult{T}"/> containing the created announcement DTO on success,
    /// or an error message on failure.
    /// </returns>
    public async Task<ApiResult<AnnouncementDto>> CreateAsync(CreateAnnouncementRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/announcements", request);
        if (response.IsSuccessStatusCode)
            return ApiResult<AnnouncementDto>.Success(await response.Content.ReadFromJsonAsync<AnnouncementDto>()!);
        return ApiResult<AnnouncementDto>.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Updates an existing announcement with the specified properties.
    /// Calls PUT /api/announcements/{id}.
    /// </summary>
    /// <param name="id">The unique identifier of the announcement to update.</param>
    /// <param name="request">The update request containing modified fields and optional ClearDismissals flag.</param>
    /// <returns>
    /// An <see cref="ApiResult{T}"/> containing the updated announcement DTO on success,
    /// or an error message on failure.
    /// </returns>
    public async Task<ApiResult<AnnouncementDto>> UpdateAsync(Guid id, UpdateAnnouncementRequest request)
    {
        var response = await _http.PutAsJsonAsync($"/api/announcements/{id}", request);
        if (response.IsSuccessStatusCode)
            return ApiResult<AnnouncementDto>.Success(await response.Content.ReadFromJsonAsync<AnnouncementDto>()!);
        return ApiResult<AnnouncementDto>.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Deletes an announcement and all associated dismissal records.
    /// Calls DELETE /api/announcements/{id}.
    /// </summary>
    /// <param name="id">The unique identifier of the announcement to delete.</param>
    /// <returns>
    /// An <see cref="ApiResult"/> indicating success or failure with an error message.
    /// </returns>
    public async Task<ApiResult> DeleteAsync(Guid id)
    {
        var response = await _http.DeleteAsync($"/api/announcements/{id}");
        if (response.IsSuccessStatusCode) return ApiResult.Success();
        return ApiResult.Failure(await response.Content.ReadAsStringAsync());
    }

    #endregion

    #region Dismissal

    /// <summary>
    /// Dismisses an announcement for the current user so it no longer appears in the banner.
    /// Calls POST /api/announcements/{id}/dismiss.
    /// </summary>
    /// <param name="id">The unique identifier of the announcement to dismiss.</param>
    /// <returns>
    /// An <see cref="ApiResult"/> indicating success or failure with an error message.
    /// </returns>
    public async Task<ApiResult> DismissAsync(Guid id)
    {
        var response = await _http.PostAsync($"/api/announcements/{id}/dismiss", null);
        if (response.IsSuccessStatusCode) return ApiResult.Success();
        return ApiResult.Failure(await response.Content.ReadAsStringAsync());
    }

    #endregion
}
