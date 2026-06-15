using System.Net.Http.Json;
using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Core.Contracts;
using AspireWebAppTemplate.Core.Contracts.Auth;
using AspireWebAppTemplate.Core.Contracts.AuditLog;
using AspireWebAppTemplate.Core.Contracts.Roles;
using AspireWebAppTemplate.Core.Contracts.Users;

namespace AspireWebAppTemplate.Web.Services;

/// <summary>
/// HTTP client service for user management operations.
/// Calls the API's UsersController endpoints.
/// </summary>
public class ApiUserService(HttpClient http)
{
    #region CRUD Operations

    /// <summary>
    /// Returns a paged list of users with optional search filtering.
    /// </summary>
    public async Task<ApiResult<PagedResult<UserDto>>> GetUsersAsync(int page, int pageSize, string? searchTerm = null)
    {
        var url = $"/api/users?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(searchTerm))
            url += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
        var response = await http.GetAsync(url);
        if (response.IsSuccessStatusCode)
            return ApiResult<PagedResult<UserDto>>.Success(await response.Content.ReadFromJsonAsync<PagedResult<UserDto>>()!);
        return ApiResult<PagedResult<UserDto>>.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Gets all users (no pagination) for client-side grid operations.
    /// </summary>
    public async Task<List<UserDto>> GetAllUsersAsync(string? searchTerm = null)
    {
        var url = "/api/users";
        if (!string.IsNullOrWhiteSpace(searchTerm))
            url += $"?searchTerm={Uri.EscapeDataString(searchTerm)}";
        var response = await http.GetAsync(url);
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<PagedResult<UserDto>>();
            return result?.Items ?? [];
        }
        return [];
    }

    /// <summary>
    /// Retrieves a single user by their unique identifier.
    /// </summary>
    public async Task<ApiResult<UserDto>> GetUserAsync(string id)
    {
        var response = await http.GetAsync($"/api/users/{id}");
        if (response.IsSuccessStatusCode)
            return ApiResult<UserDto>.Success(await response.Content.ReadFromJsonAsync<UserDto>()!);
        return ApiResult<UserDto>.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Creates a new user account with the specified details.
    /// </summary>
    public async Task<ApiResult> CreateUserAsync(CreateUserRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/users", request);
        if (response.IsSuccessStatusCode) return ApiResult.Success();
        return ApiResult.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Updates an existing user's profile information.
    /// </summary>
    public async Task<ApiResult> UpdateUserAsync(string id, UpdateUserRequest request)
    {
        var response = await http.PutAsJsonAsync($"/api/users/{id}", request);
        if (response.IsSuccessStatusCode) return ApiResult.Success();
        return ApiResult.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Deletes a user account by their unique identifier.
    /// </summary>
    public async Task<ApiResult> DeleteUserAsync(string id)
    {
        var response = await http.DeleteAsync($"/api/users/{id}");
        if (response.IsSuccessStatusCode) return ApiResult.Success();
        return ApiResult.Failure(await response.Content.ReadAsStringAsync());
    }

    #endregion

    #region Activation + Roles

    /// <summary>
    /// Activates a previously deactivated user account.
    /// </summary>
    public async Task<ApiResult> ActivateUserAsync(string id)
    {
        var response = await http.PostAsync($"/api/users/{id}/activate", null);
        if (response.IsSuccessStatusCode) return ApiResult.Success();
        return ApiResult.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Deactivates a user account, preventing login.
    /// </summary>
    public async Task<ApiResult> DeactivateUserAsync(string id)
    {
        var response = await http.PostAsync($"/api/users/{id}/deactivate", null);
        if (response.IsSuccessStatusCode) return ApiResult.Success();
        return ApiResult.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Assigns the specified roles to a user, replacing any existing role assignments.
    /// </summary>
    public async Task<ApiResult> SetRolesAsync(string id, string[] roleNames)
    {
        var response = await http.PostAsJsonAsync($"/api/users/{id}/roles", roleNames);
        if (response.IsSuccessStatusCode) return ApiResult.Success();
        return ApiResult.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Returns all roles with metadata (positions, defaults, etc.) for authority checks.
    /// </summary>
    public async Task<ApiResult<List<RoleDto>>> GetRolesMetadataAsync()
    {
        var response = await http.GetAsync("/api/users/roles-metadata");
        if (response.IsSuccessStatusCode)
            return ApiResult<List<RoleDto>>.Success(await response.Content.ReadFromJsonAsync<List<RoleDto>>()!);
        return ApiResult<List<RoleDto>>.Failure(await response.Content.ReadAsStringAsync());
    }

    #endregion

    #region LDAP Operations

    /// <summary>
    /// [LDAP] Looks up a user from Active Directory.
    /// </summary>
    public async Task<ApiResult<LdapUserAttributes>> LdapLookupAsync(string identifier)
    {
        var response = await http.GetAsync($"/api/users/ldap-lookup?identifier={Uri.EscapeDataString(identifier)}");
        if (response.IsSuccessStatusCode)
            return ApiResult<LdapUserAttributes>.Success(await response.Content.ReadFromJsonAsync<LdapUserAttributes>()!);
        return ApiResult<LdapUserAttributes>.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// [LDAP] Creates a local user from LDAP attributes.
    /// </summary>
    public async Task<ApiResult> CreateLdapUserAsync(LdapUserAttributes attributes)
    {
        var response = await http.PostAsJsonAsync("/api/users/ldap-create", attributes);
        if (response.IsSuccessStatusCode) return ApiResult.Success();
        return ApiResult.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// [LDAP] Syncs all LDAP-sourced users with Active Directory.
    /// </summary>
    public async Task<ApiResult<LdapSyncResult>> SyncLdapUsersAsync()
    {
        var response = await http.PostAsync("/api/users/ldap-sync", null);
        if (response.IsSuccessStatusCode)
            return ApiResult<LdapSyncResult>.Success(await response.Content.ReadFromJsonAsync<LdapSyncResult>()!);
        return ApiResult<LdapSyncResult>.Failure(await response.Content.ReadAsStringAsync());
    }

    #endregion
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
