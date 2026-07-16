using System.Net.Http.Json;
using AspireWebAppTemplate.Core.Common;

namespace AspireWebAppTemplate.Web.Services.ApiClients;

/// <summary>
/// Typed HTTP client service for the navigation endpoint.
/// Calls the API's NavigationController to retrieve the pre-filtered navigation tree
/// for the authenticated user. Uses Aspire service discovery ("https+http://apiservice")
/// and <see cref="UserIdentityDelegatingHandler"/> for authentication propagation.
/// </summary>
public class ApiNavigationService
{
    #region Constructor

    /// <summary>
    /// The underlying HttpClient configured with the ApiService base address.
    /// </summary>
    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of <see cref="ApiNavigationService"/> with the configured HttpClient.
    /// </summary>
    /// <param name="http">The HttpClient instance configured via Aspire service discovery.</param>
    public ApiNavigationService(HttpClient http)
    {
        _http = http;
    }

    #endregion

    /// <summary>
    /// Retrieves the filtered navigation tree for the currently authenticated user.
    /// The API applies authentication filtering, permission filtering, group visibility
    /// resolution, and orphan decoration removal before returning the result.
    /// Calls GET /api/navigation.
    /// </summary>
    /// <returns>
    /// An <see cref="ApiResult{T}"/> containing the filtered list of <see cref="NavItem"/> objects
    /// on success, or an error message on HTTP error, network failure, or deserialization failure.
    /// </returns>
    public async Task<ApiResult<List<NavItem>>> GetFilteredNavigationAsync()
    {
        try
        {
            var response = await _http.GetAsync("/api/navigation");
            if (response.IsSuccessStatusCode)
                return ApiResult<List<NavItem>>.Success(await response.Content.ReadFromJsonAsync<List<NavItem>>() ?? []);
            return ApiResult<List<NavItem>>.Failure(await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return ApiResult<List<NavItem>>.Failure(ex.Message);
        }
    }
}
