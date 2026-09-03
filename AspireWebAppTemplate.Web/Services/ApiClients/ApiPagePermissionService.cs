using System.Net.Http.Json;
using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Application.Features.Template.PagePermissions;

namespace AspireWebAppTemplate.Web.Services;

/// <summary>
/// HTTP client service for page permission operations.
/// Wraps calls to the ApiService's PagePermissionsController endpoints,
/// using Aspire service discovery ("https+http://apiservice") and
/// <see cref="UserIdentityDelegatingHandler"/> for authentication propagation.
/// </summary>
public class ApiPagePermissionService
{
    #region Constructor

    /// <summary>
    /// The underlying HttpClient configured with the ApiService base address.
    /// </summary>
    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of <see cref="ApiPagePermissionService"/> with the configured HttpClient.
    /// </summary>
    /// <param name="http">The HttpClient instance configured via Aspire service discovery.</param>
    public ApiPagePermissionService(HttpClient http)
    {
        _http = http;
    }

    #endregion

    /// <summary>
    /// Retrieves the list of page paths accessible to the currently authenticated user,
    /// based on all roles assigned to that user.
    /// Calls GET /api/page-permissions/my-pages.
    /// </summary>
    /// <returns>
    /// An <see cref="ApiResult{T}"/> containing the list of accessible page paths on success,
    /// or an error message on failure.
    /// </returns>
    public async Task<ApiResult<List<string>>> GetMyPagesAsync()
    {
        var response = await _http.GetAsync("/api/page-permissions/my-pages");
        if (response.IsSuccessStatusCode)
            return ApiResult<List<string>>.Success(await response.Content.ReadFromJsonAsync<List<string>>() ?? []);
        return ApiResult<List<string>>.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Retrieves all page permissions grouped by role.
    /// Calls GET /api/page-permissions. Requires the "Admin" role.
    /// </summary>
    /// <returns>
    /// An <see cref="ApiResult{T}"/> containing the list of <see cref="RolePermissionsDto"/> on success,
    /// or an error message on failure.
    /// </returns>
    public async Task<ApiResult<List<RolePermissionsDto>>> GetAllPermissionsAsync()
    {
        var response = await _http.GetAsync("/api/page-permissions");
        if (response.IsSuccessStatusCode)
            return ApiResult<List<RolePermissionsDto>>.Success(await response.Content.ReadFromJsonAsync<List<RolePermissionsDto>>() ?? []);
        return ApiResult<List<RolePermissionsDto>>.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Updates the page permissions for a specific role, replacing all existing permissions
    /// with the provided list of page paths.
    /// Calls PUT /api/page-permissions/{roleId}. Requires the "Admin" role.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role to update.</param>
    /// <param name="request">The request containing the complete list of page paths to grant.</param>
    /// <returns>
    /// An <see cref="ApiResult"/> indicating success or failure with an error message.
    /// </returns>
    public async Task<ApiResult> UpdateRolePermissionsAsync(string roleId, UpdateRolePermissionsRequest request)
    {
        var response = await _http.PutAsJsonAsync($"/api/page-permissions/{roleId}", request);
        if (response.IsSuccessStatusCode) return ApiResult.Success();
        return ApiResult.Failure(await response.Content.ReadAsStringAsync());
    }
}
