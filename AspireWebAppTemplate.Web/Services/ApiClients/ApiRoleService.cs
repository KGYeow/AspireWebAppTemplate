using System.Net.Http.Json;
using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Application.Features.Authentication;
using AspireWebAppTemplate.Application.Features.AuditLog;
using AspireWebAppTemplate.Application.Features.Roles;
using AspireWebAppTemplate.Application.Features.Users;
using AspireWebAppTemplate.Web.Extensions;

namespace AspireWebAppTemplate.Web.Services;

/// <summary>
/// HTTP client service for role management operations.
/// Calls the API's RolesController endpoints.
/// </summary>
public class ApiRoleService
{
    #region Constructor

    /// <summary>
    /// The underlying HttpClient configured with the ApiService base address.
    /// </summary>
    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of <see cref="ApiRoleService"/> with the configured HttpClient.
    /// </summary>
    /// <param name="http">The HttpClient instance configured via Aspire service discovery.</param>
    public ApiRoleService(HttpClient http)
    {
        _http = http;
    }

    #endregion

    #region CRUD Operations

    /// <summary>
    /// Retrieves all roles in the system.
    /// </summary>
    public async Task<ApiResult<List<RoleDto>>> GetRolesAsync()
    {
        var response = await _http.GetAsync("/api/roles");
        return await response.ToApiResultAsync<List<RoleDto>>();
    }

    /// <summary>
    /// Retrieves a single role by its unique identifier.
    /// </summary>
    public async Task<ApiResult<RoleDto>> GetRoleAsync(string id)
    {
        var response = await _http.GetAsync($"/api/roles/{id}");
        return await response.ToApiResultAsync<RoleDto>();
    }

    /// <summary>
    /// Creates a new role with the specified name and permissions.
    /// </summary>
    public async Task<ApiResult> CreateRoleAsync(CreateRoleRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/roles", request);
        return await response.ToApiResultAsync();
    }

    /// <summary>
    /// Updates an existing role's name or permissions.
    /// </summary>
    public async Task<ApiResult> UpdateRoleAsync(string id, CreateRoleRequest request)
    {
        var response = await _http.PutAsJsonAsync($"/api/roles/{id}", request);
        return await response.ToApiResultAsync();
    }

    /// <summary>
    /// Deletes a role by its unique identifier.
    /// </summary>
    public async Task<ApiResult> DeleteRoleAsync(string id)
    {
        var response = await _http.DeleteAsync($"/api/roles/{id}");
        return await response.ToApiResultAsync();
    }

    #endregion

    #region Activation

    /// <summary>
    /// Activates a previously deactivated role.
    /// </summary>
    public async Task<ApiResult> ActivateRoleAsync(string id)
    {
        var response = await _http.PostAsync($"/api/roles/{id}/activate", null);
        return await response.ToApiResultAsync();
    }

    /// <summary>
    /// Deactivates a role, preventing it from being assigned.
    /// </summary>
    public async Task<ApiResult> DeactivateRoleAsync(string id)
    {
        var response = await _http.PostAsync($"/api/roles/{id}/deactivate", null);
        return await response.ToApiResultAsync();
    }

    #endregion

    #region User-Role Assignment

    /// <summary>
    /// Returns all users assigned to the specified role.
    /// </summary>
    public async Task<ApiResult<List<UserDto>>> GetUsersInRoleAsync(string id)
    {
        var response = await _http.GetAsync($"/api/roles/{id}/users");
        return await response.ToApiResultAsync<List<UserDto>>();
    }

    /// <summary>
    /// Assigns multiple users to a role.
    /// </summary>
    public async Task<ApiResult> AssignUsersToRoleAsync(string roleId, string[] userIds)
    {
        var response = await _http.PostAsJsonAsync($"/api/roles/{roleId}/users", userIds);
        return await response.ToApiResultAsync();
    }

    /// <summary>
    /// Removes a user from a role.
    /// </summary>
    public async Task<ApiResult> RemoveUserFromRoleAsync(string roleId, string userId)
    {
        var response = await _http.DeleteAsync($"/api/roles/{roleId}/users/{userId}");
        return await response.ToApiResultAsync();
    }

    #endregion
}
