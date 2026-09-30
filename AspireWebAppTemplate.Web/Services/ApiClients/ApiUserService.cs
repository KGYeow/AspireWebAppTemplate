using System.Net.Http.Json;
using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Application.Features.Authentication;
using AspireWebAppTemplate.Application.Features.AuditLog;
using AspireWebAppTemplate.Application.Features.Roles;
using AspireWebAppTemplate.Application.Features.Users;
using AspireWebAppTemplate.Web.Extensions;
using AspireWebAppTemplate.Web.Utilities;

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
        var url = QueryStringBuilder.Build("/api/users", qs =>
        {
            qs.AddIfHasValue("page", queryParams.Page);
            qs.AddIfHasValue("pageSize", queryParams.PageSize);
            qs.AddIfNotWhiteSpace("searchTerm", queryParams.SearchTerm);
        });

        var response = await _http.GetAsync(url);
        return await response.ToApiResultAsync<PagedResult<UserDto>>();
    }

    /// <summary>
    /// Gets all users (no pagination) for client-side grid operations.
    /// </summary>
    public async Task<List<UserDto>> GetAllUsersAsync(string? searchTerm = null)
    {
        var url = QueryStringBuilder.Build("/api/users", qs =>
        {
            qs.AddIfNotWhiteSpace("searchTerm", searchTerm);
        });

        var response = await _http.GetAsync(url);
        var result = await response.ToApiResultAsync<PagedResult<UserDto>>(defaultValue: new());
        return result.Data?.Items ?? [];
    }

    /// <summary>
    /// Retrieves a single user by their unique identifier.
    /// </summary>
    public async Task<ApiResult<UserDto>> GetUserAsync(string id)
    {
        var response = await _http.GetAsync($"/api/users/{id}");
        return await response.ToApiResultAsync<UserDto>();
    }

    /// <summary>
    /// Creates a new user account with the specified details.
    /// </summary>
    public async Task<ApiResult> CreateUserAsync(CreateUserRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/users", request);
        return await response.ToApiResultAsync();
    }

    /// <summary>
    /// Updates an existing user's profile information.
    /// </summary>
    public async Task<ApiResult> UpdateUserAsync(string id, UpdateUserRequest request)
    {
        var response = await _http.PutAsJsonAsync($"/api/users/{id}", request);
        return await response.ToApiResultAsync();
    }

    /// <summary>
    /// Deletes a user account by their unique identifier.
    /// </summary>
    public async Task<ApiResult> DeleteUserAsync(string id)
    {
        var response = await _http.DeleteAsync($"/api/users/{id}");
        return await response.ToApiResultAsync();
    }

    #endregion

    #region Activation + Roles

    /// <summary>
    /// Activates a previously deactivated user account.
    /// </summary>
    public async Task<ApiResult> ActivateUserAsync(string id)
    {
        var response = await _http.PostAsync($"/api/users/{id}/activate", null);
        return await response.ToApiResultAsync();
    }

    /// <summary>
    /// Deactivates a user account, preventing login.
    /// </summary>
    public async Task<ApiResult> DeactivateUserAsync(string id)
    {
        var response = await _http.PostAsync($"/api/users/{id}/deactivate", null);
        return await response.ToApiResultAsync();
    }

    /// <summary>
    /// Assigns the specified roles to a user, replacing any existing role assignments.
    /// </summary>
    public async Task<ApiResult> SetRolesAsync(string id, string[] roleNames)
    {
        var response = await _http.PostAsJsonAsync($"/api/users/{id}/roles", roleNames);
        return await response.ToApiResultAsync();
    }

    /// <summary>
    /// Returns all roles with metadata (positions, defaults, etc.) for authority checks.
    /// </summary>
    public async Task<ApiResult<List<RoleDto>>> GetRolesMetadataAsync()
    {
        var response = await _http.GetAsync("/api/users/roles-metadata");
        return await response.ToApiResultAsync<List<RoleDto>>();
    }

    #endregion

    #region LDAP Operations

    /// <summary>
    /// [LDAP] Looks up a user from Active Directory.
    /// </summary>
    public async Task<ApiResult<LdapUserAttributes>> LdapLookupAsync(string identifier)
    {
        var response = await _http.GetAsync($"/api/users/ldap-lookup?identifier={Uri.EscapeDataString(identifier)}");
        return await response.ToApiResultAsync<LdapUserAttributes>();
    }

    /// <summary>
    /// [LDAP] Creates a local user from LDAP attributes.
    /// </summary>
    public async Task<ApiResult> CreateLdapUserAsync(LdapUserAttributes attributes)
    {
        var response = await _http.PostAsJsonAsync("/api/users/ldap-create", attributes);
        return await response.ToApiResultAsync();
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
