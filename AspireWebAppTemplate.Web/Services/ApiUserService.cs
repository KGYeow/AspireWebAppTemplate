using System.Net.Http.Json;
using AspireWebAppTemplate.Core.Contracts;

namespace AspireWebAppTemplate.Web.Services;

/// <summary>
/// HTTP client service for user management operations.
/// Calls the API's UsersController endpoints.
/// </summary>
public class ApiUserService(HttpClient http)
{
    public async Task<PagedResult<UserDto>?> GetUsersAsync(int page = 0, int pageSize = 10, string? searchTerm = null)
    {
        var url = $"/api/users?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(searchTerm))
            url += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
        return await http.GetFromJsonAsync<PagedResult<UserDto>>(url);
    }

    /// <summary>
    /// Gets all users (large page size) for client-side grid operations.
    /// </summary>
    public async Task<List<UserDto>> GetAllUsersAsync(string? searchTerm = null)
    {
        var url = $"/api/users?page=0&pageSize=10000";
        if (!string.IsNullOrWhiteSpace(searchTerm))
            url += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
        var result = await http.GetFromJsonAsync<PagedResult<UserDto>>(url);
        return result?.Items ?? [];
    }

    public async Task<UserDto?> GetUserAsync(string id)
        => await http.GetFromJsonAsync<UserDto>($"/api/users/{id}");

    public async Task<(bool Success, string? Error)> CreateUserAsync(CreateUserRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/users", request);
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<(bool Success, string? Error)> UpdateUserAsync(string id, UpdateUserRequest request)
    {
        var response = await http.PutAsJsonAsync($"/api/users/{id}", request);
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<(bool Success, string? Error)> DeleteUserAsync(string id)
    {
        var response = await http.DeleteAsync($"/api/users/{id}");
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<bool> ActivateUserAsync(string id)
        => (await http.PostAsync($"/api/users/{id}/activate", null)).IsSuccessStatusCode;

    public async Task<bool> DeactivateUserAsync(string id)
        => (await http.PostAsync($"/api/users/{id}/deactivate", null)).IsSuccessStatusCode;

    public async Task<(bool Success, string? Error)> SetRolesAsync(string id, string[] roleNames)
    {
        var response = await http.PostAsJsonAsync($"/api/users/{id}/roles", roleNames);
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Returns all roles with metadata (positions, defaults, etc.) for authority checks.
    /// </summary>
    public async Task<List<RoleDto>?> GetRolesMetadataAsync()
        => await http.GetFromJsonAsync<List<RoleDto>>("/api/users/roles-metadata");

    /// <summary>
    /// [LDAP] Looks up a user from Active Directory.
    /// </summary>
    public async Task<LdapUserAttributes?> LdapLookupAsync(string identifier)
    {
        var response = await http.GetAsync($"/api/users/ldap-lookup?identifier={Uri.EscapeDataString(identifier)}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<LdapUserAttributes>();
    }

    /// <summary>
    /// [LDAP] Creates a local user from LDAP attributes.
    /// </summary>
    public async Task<(bool Success, string? Error)> CreateLdapUserAsync(LdapUserAttributes attributes)
    {
        var response = await http.PostAsJsonAsync("/api/users/ldap-create", attributes);
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// [LDAP] Syncs all LDAP-sourced users with Active Directory.
    /// </summary>
    public async Task<LdapSyncResult?> SyncLdapUsersAsync()
    {
        var response = await http.PostAsync("/api/users/ldap-sync", null);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<LdapSyncResult>();
    }
}

/// <summary>
/// Result returned by the LDAP sync API.
/// </summary>
public sealed class LdapSyncResult
{
    public int Total { get; set; }
    public int Updated { get; set; }
    public int Failed { get; set; }
}
