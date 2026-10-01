using System.Net.Http.Json;
using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Application.Features.Permissions;
using AspireWebAppTemplate.Web.Extensions;

namespace AspireWebAppTemplate.Web.Services;

/// <summary>
/// HTTP client service for resource-based (<c>Module.Action</c>) permission operations.
/// Wraps calls to the ApiService's PermissionController endpoints, using Aspire service discovery
/// ("https+http://apiservice") and <see cref="UserIdentityDelegatingHandler"/> for authentication propagation.
/// </summary>
/// <remarks>
/// All methods return an <see cref="ApiResult"/> / <see cref="ApiResult{T}"/> and never throw on HTTP
/// errors: the response is mapped via <see cref="HttpResponseMessageExtensions.ToApiResultAsync{T}"/>,
/// which carries the response body text as the error message on a non-success status.
/// </remarks>
public class ApiPermissionService
{
    #region Constructor

    /// <summary>
    /// The underlying HttpClient configured with the ApiService base address.
    /// </summary>
    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of <see cref="ApiPermissionService"/> with the configured HttpClient.
    /// </summary>
    /// <param name="http">The HttpClient instance configured via Aspire service discovery.</param>
    public ApiPermissionService(HttpClient http)
    {
        _http = http;
    }

    #endregion

    #region Query Operations

    /// <summary>
    /// Retrieves every defined permission grouped by its owning module, so the management UI can
    /// render the permission matrix with one row group per module.
    /// Calls GET /api/permissions. Requires the "Permissions.Manage" permission.
    /// </summary>
    /// <returns>
    /// An <see cref="ApiResult{T}"/> containing the list of <see cref="PermissionGroupDto"/> on success,
    /// or an error message on failure.
    /// </returns>
    public async Task<ApiResult<List<PermissionGroupDto>>> GetAllPermissionsAsync()
    {
        var response = await _http.GetAsync("/api/permissions");
        return await response.ToApiResultAsync<List<PermissionGroupDto>>();
    }

    /// <summary>
    /// Retrieves the permission keys currently granted to the specified role.
    /// Calls GET /api/permissions/roles/{roleId}. Requires the "Permissions.Manage" permission.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role whose permissions are being queried.</param>
    /// <returns>
    /// An <see cref="ApiResult{T}"/> containing the <see cref="RolePermissionsDto"/> on success,
    /// or an error message on failure.
    /// </returns>
    public async Task<ApiResult<RolePermissionsDto>> GetRolePermissionsAsync(string roleId)
    {
        var response = await _http.GetAsync($"/api/permissions/roles/{roleId}");
        return await response.ToApiResultAsync<RolePermissionsDto>();
    }

    /// <summary>
    /// Retrieves the effective permission keys for the currently authenticated user, computed as the
    /// union of all permissions granted to the user's assigned roles.
    /// Calls GET /api/permissions/my-permissions. Reachable by any authenticated user.
    /// </summary>
    /// <returns>
    /// An <see cref="ApiResult{T}"/> containing the list of effective permission keys on success,
    /// or an error message on failure.
    /// </returns>
    public async Task<ApiResult<List<string>>> GetMyPermissionsAsync()
    {
        var response = await _http.GetAsync("/api/permissions/my-permissions");
        return await response.ToApiResultAsync<List<string>>();
    }

    /// <summary>
    /// Retrieves the static mapping of admin page paths to the module that gates each page.
    /// Calls GET /api/permissions/page-modules. Reachable by any authenticated user.
    /// </summary>
    /// <returns>
    /// An <see cref="ApiResult{T}"/> containing the list of <see cref="PageModuleMappingDto"/> on success,
    /// or an error message on failure.
    /// </returns>
    public async Task<ApiResult<List<PageModuleMappingDto>>> GetPageModuleMappingsAsync()
    {
        var response = await _http.GetAsync("/api/permissions/page-modules");
        return await response.ToApiResultAsync<List<PageModuleMappingDto>>();
    }

    #endregion

    #region Write Operations

    /// <summary>
    /// Updates the permissions granted to a specific role, replacing all existing grants with the
    /// provided list of permission keys (full-replacement strategy; an empty list clears all grants).
    /// Calls PUT /api/permissions/roles/{roleId}. Requires the "Permissions.Manage" permission.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role to update.</param>
    /// <param name="request">The request containing the complete list of permission keys to grant.</param>
    /// <returns>
    /// An <see cref="ApiResult"/> indicating success or failure with an error message.
    /// </returns>
    public async Task<ApiResult> UpdateRolePermissionsAsync(string roleId, UpdateRolePermissionsRequest request)
    {
        var response = await _http.PutAsJsonAsync($"/api/permissions/roles/{roleId}", request);
        return await response.ToApiResultAsync();
    }

    #endregion
}
