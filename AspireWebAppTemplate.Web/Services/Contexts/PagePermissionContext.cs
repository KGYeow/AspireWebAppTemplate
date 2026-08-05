using AspireWebAppTemplate.Domain.Constants;
using AspireWebAppTemplate.Web.Abstractions;
using Microsoft.AspNetCore.Components.Authorization;

namespace AspireWebAppTemplate.Web.Services;

/// <summary>
/// Per-circuit permission cache that loads the authenticated user's accessible page paths
/// once during circuit initialization and provides synchronous O(1) lookups for navigation
/// authorization checks.
/// </summary>
/// <remarks>
/// <para>
/// <b>Per-circuit caching strategy:</b> This service is registered as <b>scoped</b>, meaning each
/// Blazor Server SignalR circuit (user session) gets its own instance. Permissions are loaded a single
/// time via <see cref="InitializeAsync"/> and remain cached for the lifetime of the circuit. This
/// eliminates repeated API calls on every navigation event. Users must refresh or start a new session
/// to pick up permission changes made by an administrator.
/// </para>
/// <para>
/// <b>O(1) lookup rationale:</b> Page paths are stored in a <see cref="HashSet{T}"/> with
/// <see cref="StringComparer.OrdinalIgnoreCase"/> to guarantee constant-time membership checks.
/// This ensures that the <see cref="CanAccess"/> method introduces no measurable latency during
/// page navigation, regardless of how many pages are granted to the user.
/// </para>
/// <para>
/// <b>System_Pages bypass:</b> Certain pages (Login, Register, AccessDenied, Error, ForgotPassword,
/// ResetPassword, PerformLogin) must always be accessible to all users — authenticated or not.
/// These are checked first in <see cref="CanAccess"/> to short-circuit the HashSet lookup and ensure
/// the application remains navigable even when the permission cache is empty or uninitialized.
/// </para>
/// </remarks>
public sealed class PagePermissionContext : IPagePermissionContext
{
    private readonly ApiPagePermissionService _apiService;
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly ILogger<PagePermissionContext> _logger;

    /// <summary>
    /// The set of page paths accessible to the current user, populated during initialization.
    /// Uses OrdinalIgnoreCase comparison for case-insensitive O(1) membership tests.
    /// </summary>
    private HashSet<string> _accessiblePages = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Tracks whether <see cref="InitializeAsync"/> has completed (success or failure).
    /// When false, <see cref="CanAccess"/> denies all non-System_Pages to prevent
    /// unauthorized access before permissions are loaded.
    /// </summary>
    private bool _isLoaded;

    /// <summary>
    /// Initializes a new instance of <see cref="PagePermissionContext"/>.
    /// </summary>
    /// <param name="apiService">
    /// The HTTP client wrapper for calling the page permissions API endpoint.
    /// </param>
    /// <param name="authStateProvider">
    /// The Blazor authentication state provider used to determine whether the current
    /// user is authenticated before making API calls.
    /// </param>
    /// <param name="logger">
    /// Logger for recording initialization failures and diagnostic information.
    /// </param>
    public PagePermissionContext(
        ApiPagePermissionService apiService,
        AuthenticationStateProvider authStateProvider,
        ILogger<PagePermissionContext> logger)
    {
        _apiService = apiService;
        _authStateProvider = authStateProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsLoaded => _isLoaded;

    /// <inheritdoc />
    /// <remarks>
    /// Evaluation order:
    /// 1. System_Pages — always return true (authentication/error pages must remain accessible)
    /// 2. Not loaded — return false to block access until cache is populated
    /// 3. HashSet membership — O(1) case-insensitive lookup against cached permissions
    /// </remarks>
    public bool CanAccess(string pagePath)
    {
        // System_Pages bypass: these pages are always accessible regardless of
        // cache state or user permissions (login, register, error pages, etc.)
        if (SystemPageDefaults.Paths.Contains(pagePath))
            return true;

        // If permissions have not yet been loaded, deny access to non-System_Pages
        // to prevent unauthorized navigation before initialization completes
        if (!_isLoaded)
            return false;

        // O(1) HashSet lookup using OrdinalIgnoreCase comparison
        return _accessiblePages.Contains(pagePath);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Returns the cached list of accessible page paths as a read-only snapshot.
    /// The list does not include System_Pages (those are always granted implicitly).
    /// Returns an empty list if permissions have not been loaded or the API call failed.
    /// </remarks>
    public IReadOnlyList<string> GetAccessiblePages()
    {
        return _accessiblePages.ToList().AsReadOnly();
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Called once per circuit during initialization (typically from MainLayout.OnInitializedAsync).
    /// </para>
    /// <para>
    /// If the user is unauthenticated, the method skips the API call entirely and leaves the
    /// cache empty — unauthenticated users only have access to System_Pages.
    /// </para>
    /// <para>
    /// On API failure, the cache remains empty and <see cref="CanAccess"/> returns false for
    /// all non-System_Pages. A warning is logged to aid diagnostics. The <see cref="IsLoaded"/>
    /// property is still set to true so that the application does not remain in a perpetual
    /// "loading" state.
    /// </para>
    /// </remarks>
    public async Task InitializeAsync()
    {
        try
        {
            // Check authentication state — skip API call for unauthenticated users.
            // Unauthenticated users only have access to System_Pages (handled by CanAccess).
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            if (authState.User.Identity?.IsAuthenticated != true)
            {
                // No API call needed; cache stays empty, IsLoaded set to true
                _isLoaded = true;
                return;
            }

            // Single API call to load all accessible page paths for the current user.
            // The API returns the union of permissions across all roles assigned to the user.
            var result = await _apiService.GetMyPagesAsync();

            if (result.Succeeded && result.Data is not null)
            {
                // Populate the HashSet with OrdinalIgnoreCase for case-insensitive O(1) lookups
                _accessiblePages = new HashSet<string>(result.Data, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                // API call returned a non-success result — keep cache empty and log warning.
                // CanAccess will return false for all non-System_Pages.
                _logger.LogWarning(
                    "Failed to load page permissions from API. Error: {Error}. " +
                    "User will only have access to System_Pages until next circuit initialization.",
                    result.Error);
                _accessiblePages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex)
        {
            // Network failure or unexpected error — keep cache empty and log warning.
            // The application remains usable for System_Pages; user can refresh to retry.
            _logger.LogWarning(ex,
                "Exception occurred while loading page permissions. " +
                "User will only have access to System_Pages until next circuit initialization.");
            _accessiblePages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            // Always mark as loaded (even on failure) so the UI transitions out of
            // the loading state and doesn't leave the user stuck on a loading skeleton
            _isLoaded = true;
        }
    }
}
