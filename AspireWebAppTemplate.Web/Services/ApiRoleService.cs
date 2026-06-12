using System.Net.Http.Json;
using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Core.Contracts;

namespace AspireWebAppTemplate.Web.Services;

/// <summary>
/// HTTP client service for role management operations.
/// Calls the API's RolesController endpoints.
/// </summary>
public class ApiRoleService(HttpClient http)
{
    #region CRUD Operations

    /// <summary>
    /// Retrieves all roles in the system.
    /// </summary>
    public async Task<ApiResult<List<RoleDto>>> GetRolesAsync()
    {
        var response = await http.GetAsync("/api/roles");
        if (response.IsSuccessStatusCode)
            return ApiResult<List<RoleDto>>.Success(await response.Content.ReadFromJsonAsync<List<RoleDto>>()!);
        return ApiResult<List<RoleDto>>.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Retrieves a single role by its unique identifier.
    /// </summary>
    public async Task<ApiResult<RoleDto>> GetRoleAsync(string id)
    {
        var response = await http.GetAsync($"/api/roles/{id}");
        if (response.IsSuccessStatusCode)
            return ApiResult<RoleDto>.Success(await response.Content.ReadFromJsonAsync<RoleDto>()!);
        return ApiResult<RoleDto>.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Creates a new role with the specified name and permissions.
    /// </summary>
    public async Task<ApiResult> CreateRoleAsync(CreateRoleRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/roles", request);
        if (response.IsSuccessStatusCode) return ApiResult.Success();
        return ApiResult.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Updates an existing role's name or permissions.
    /// </summary>
    public async Task<ApiResult> UpdateRoleAsync(string id, CreateRoleRequest request)
    {
        var response = await http.PutAsJsonAsync($"/api/roles/{id}", request);
        if (response.IsSuccessStatusCode) return ApiResult.Success();
        return ApiResult.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Deletes a role by its unique identifier.
    /// </summary>
    public async Task<ApiResult> DeleteRoleAsync(string id)
    {
        var response = await http.DeleteAsync($"/api/roles/{id}");
        if (response.IsSuccessStatusCode) return ApiResult.Success();
        return ApiResult.Failure(await response.Content.ReadAsStringAsync());
    }

    #endregion

    #region Activation

    /// <summary>
    /// Activates a previously deactivated role.
    /// </summary>
    public async Task<ApiResult> ActivateRoleAsync(string id)
    {
        var response = await http.PostAsync($"/api/roles/{id}/activate", null);
        if (response.IsSuccessStatusCode) return ApiResult.Success();
        return ApiResult.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Deactivates a role, preventing it from being assigned.
    /// </summary>
    public async Task<ApiResult> DeactivateRoleAsync(string id)
    {
        var response = await http.PostAsync($"/api/roles/{id}/deactivate", null);
        if (response.IsSuccessStatusCode) return ApiResult.Success();
        return ApiResult.Failure(await response.Content.ReadAsStringAsync());
    }

    #endregion

    #region User-Role Assignment

    /// <summary>
    /// Returns all users assigned to the specified role.
    /// </summary>
    public async Task<ApiResult<List<UserDto>>> GetUsersInRoleAsync(string id)
    {
        var response = await http.GetAsync($"/api/roles/{id}/users");
        if (response.IsSuccessStatusCode)
            return ApiResult<List<UserDto>>.Success(await response.Content.ReadFromJsonAsync<List<UserDto>>()!);
        return ApiResult<List<UserDto>>.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Assigns multiple users to a role.
    /// </summary>
    public async Task<ApiResult> AssignUsersToRoleAsync(string roleId, string[] userIds)
    {
        var response = await http.PostAsJsonAsync($"/api/roles/{roleId}/users", userIds);
        if (response.IsSuccessStatusCode) return ApiResult.Success();
        return ApiResult.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Removes a user from a role.
    /// </summary>
    public async Task<ApiResult> RemoveUserFromRoleAsync(string roleId, string userId)
    {
        var response = await http.DeleteAsync($"/api/roles/{roleId}/users/{userId}");
        if (response.IsSuccessStatusCode) return ApiResult.Success();
        return ApiResult.Failure(await response.Content.ReadAsStringAsync());
    }

    #endregion
}
