using System.Net.Http.Json;
using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Application.Contracts.Auth;
using AspireWebAppTemplate.Application.Features.Template.AuditLog.Contracts;
using AspireWebAppTemplate.Application.Contracts.Roles;
using AspireWebAppTemplate.Application.Contracts.Users;

namespace AspireWebAppTemplate.Web.Services;

/// <summary>
/// HTTP client service for user management operations.
/// Calls the API's UsersController endpoints.
/// </summary>
public class ApiUserService
{
    #region Constructor

    /// <summary>
    /// The underlying HttpClient configured with the ApiService base address.
    /// </summary>
    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of <see cref="ApiUserService"/> with the configured HttpClient.
    /// </summary>
    /// <param name="http">The HttpClient instance configured via Aspire service discovery.</param>
    public ApiUserService(HttpClient http)
    {
        _http = http;
    }

    #endregion

    #region CRUD Operations

    /// <summary>
    /// Returns a paged list of users with optional search filtering.
    /// </summary>
    /// <param name="queryParams">Query parameters containing page index, page size, and optional search term.</param>
    public async Task<ApiResult<PagedResult<UserDto>>> GetUsersAsync(UserQueryParams queryParams)
    {
        var queryStringParts = new List<string>();
        if (queryParams.Page.HasValue)
            queryStringParts.Add($"page={queryParams.Page.Value}");
        if (queryParams.PageSize.HasValue)
            queryStringParts.Add($"pageSize={queryParams.PageSize.Value}");
        if (!string.IsNullOrWhiteSpace(queryParams.SearchTerm))
            queryStringParts.Add($"searchTerm={Uri.EscapeDataString(queryParams.SearchTerm)}");
        var url = queryStringParts.Count > 0 ? $"/api/users?{string.Join("&", queryStringParts)}" : "/api/users";
        var response = await _http.GetAsync(url);
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
        var response = await _http.GetAsync(url);
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
        var response = await _http.GetAsync($"/api/users/{id}");
        if (response.IsSuccessStatusCode)
            return ApiResult<UserDto>.Success(await response.Content.ReadFromJsonAsync<UserDto>()!);
        return ApiResult<UserDto>.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Creates a new user account with the specified details.
    /// </summary>
    public async Task<ApiResult> CreateUserAsync(CreateUserRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/users", request);
        if (response.IsSuccessStatusCode) return ApiResult.Success();
        return ApiResult.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Updates an existing user's profile information.
    /// </summary>
    public async Task<ApiResult> UpdateUserAsync(string id, UpdateUserRequest request)
    {
        var response = await _http.PutAsJsonAsync($"/api/users/{id}", request);
        if (response.IsSuccessStatusCode) return ApiResult.Success();
        return ApiResult.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Deletes a user account by their unique identifier.
    /// </summary>
    public async Task<ApiResult> DeleteUserAsync(string id)
    {
        var response = await _http.DeleteAsync($"/api/users/{id}");
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
        var response = await _http.PostAsync($"/api/users/{id}/activate", null);
        if (response.IsSuccessStatusCode) return ApiResult.Success();
        return ApiResult.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Deactivates a user account, preventing login.
    /// </summary>
    public async Task<ApiResult> DeactivateUserAsync(string id)
    {
        var response = await _http.PostAsync($"/api/users/{id}/deactivate", null);
        if (response.IsSuccessStatusCode) return ApiResult.Success();
        return ApiResult.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Assigns the specified roles to a user, replacing any existing role assignments.
    /// </summary>
    public async Task<ApiResult> SetRolesAsync(string id, string[] roleNames)
    {
        var response = await _http.PostAsJsonAsync($"/api/users/{id}/roles", roleNames);
        if (response.IsSuccessStatusCode) return ApiResult.Success();
        return ApiResult.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Returns all roles with metadata (positions, defaults, etc.) for authority checks.
    /// </summary>
    public async Task<ApiResult<List<RoleDto>>> GetRolesMetadataAsync()
    {
        var response = await _http.GetAsync("/api/users/roles-metadata");
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
        var response = await _http.GetAsync($"/api/users/ldap-lookup?identifier={Uri.EscapeDataString(identifier)}");
        if (response.IsSuccessStatusCode)
            return ApiResult<LdapUserAttributes>.Success(await response.Content.ReadFromJsonAsync<LdapUserAttributes>()!);
        return ApiResult<LdapUserAttributes>.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// [LDAP] Creates a local user from LDAP attributes.
    /// </summary>
    public async Task<ApiResult> CreateLdapUserAsync(LdapUserAttributes attributes)
    {
        var response = await _http.PostAsJsonAsync("/api/users/ldap-create", attributes);
        if (response.IsSuccessStatusCode) return ApiResult.Success();
        return ApiResult.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// [LDAP] Syncs all LDAP-sourced users with Active Directory.
    /// Streams progress items (NDJSON) for real-time UI updates.
    /// </summary>
    public async IAsyncEnumerable<LdapSyncProgressItem?> SyncLdapUsersStreamAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/users/ldap-sync");
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
            yield break;

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new System.IO.StreamReader(stream);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line)) continue;

            var item = System.Text.Json.JsonSerializer.Deserialize<LdapSyncProgressItem>(line);
            yield return item;
        }
    }

    #endregion
}