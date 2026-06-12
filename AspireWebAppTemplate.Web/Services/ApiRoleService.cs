using System.Net.Http.Json;
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
    public async Task<List<RoleDto>?> GetRolesAsync()
        => await http.GetFromJsonAsync<List<RoleDto>>("/api/roles");

    /// <summary>
    /// Retrieves a single role by its unique identifier.
    /// </summary>
    public async Task<RoleDto?> GetRoleAsync(string id)
        => await http.GetFromJsonAsync<RoleDto>($"/api/roles/{id}");

    /// <summary>
    /// Creates a new role with the specified name and permissions.
    /// </summary>
    public async Task<(bool Success, string? Error)> CreateRoleAsync(CreateRoleRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/roles", request);
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Updates an existing role's name or permissions.
    /// </summary>
    public async Task<(bool Success, string? Error)> UpdateRoleAsync(string id, CreateRoleRequest request)
    {
        var response = await http.PutAsJsonAsync($"/api/roles/{id}", request);
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Deletes a role by its unique identifier.
    /// </summary>
    public async Task<(bool Success, string? Error)> DeleteRoleAsync(string id)
    {
        var response = await http.DeleteAsync($"/api/roles/{id}");
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    #endregion

    #region Activation

    /// <summary>
    /// Activates a previously deactivated role.
    /// </summary>
    public async Task<bool> ActivateRoleAsync(string id)
        => (await http.PostAsync($"/api/roles/{id}/activate", null)).IsSuccessStatusCode;

    /// <summary>
    /// Deactivates a role, preventing it from being assigned.
    /// </summary>
    public async Task<bool> DeactivateRoleAsync(string id)
        => (await http.PostAsync($"/api/roles/{id}/deactivate", null)).IsSuccessStatusCode;

    #endregion

    #region User-Role Assignment

    /// <summary>
    /// Returns all users assigned to the specified role.
    /// </summary>
    public async Task<List<UserDto>?> GetUsersInRoleAsync(string id)
        => await http.GetFromJsonAsync<List<UserDto>>($"/api/roles/{id}/users");

    /// <summary>
    /// Assigns multiple users to a role.
    /// </summary>
    public async Task<(bool Success, string? Error)> AssignUsersToRoleAsync(string roleId, string[] userIds)
    {
        var response = await http.PostAsJsonAsync($"/api/roles/{roleId}/users", userIds);
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Removes a user from a role.
    /// </summary>
    public async Task<(bool Success, string? Error)> RemoveUserFromRoleAsync(string roleId, string userId)
    {
        var response = await http.DeleteAsync($"/api/roles/{roleId}/users/{userId}");
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    #endregion
}
