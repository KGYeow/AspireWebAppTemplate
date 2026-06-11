using System.Net.Http.Json;
using AspireWebAppTemplate.Core.Contracts;

namespace AspireWebAppTemplate.Web.Services;

/// <summary>
/// HTTP client service for role management operations.
/// Calls the API's RolesController endpoints.
/// </summary>
public class ApiRoleService(HttpClient http)
{
    public async Task<List<RoleDto>?> GetRolesAsync()
        => await http.GetFromJsonAsync<List<RoleDto>>("/api/roles");

    public async Task<RoleDto?> GetRoleAsync(string id)
        => await http.GetFromJsonAsync<RoleDto>($"/api/roles/{id}");

    public async Task<(bool Success, string? Error)> CreateRoleAsync(CreateRoleRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/roles", request);
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<(bool Success, string? Error)> UpdateRoleAsync(string id, CreateRoleRequest request)
    {
        var response = await http.PutAsJsonAsync($"/api/roles/{id}", request);
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<(bool Success, string? Error)> DeleteRoleAsync(string id)
    {
        var response = await http.DeleteAsync($"/api/roles/{id}");
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<List<UserDto>?> GetUsersInRoleAsync(string id)
        => await http.GetFromJsonAsync<List<UserDto>>($"/api/roles/{id}/users");

    public async Task<bool> ActivateRoleAsync(string id)
        => (await http.PostAsync($"/api/roles/{id}/activate", null)).IsSuccessStatusCode;

    public async Task<bool> DeactivateRoleAsync(string id)
        => (await http.PostAsync($"/api/roles/{id}/deactivate", null)).IsSuccessStatusCode;

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
}
