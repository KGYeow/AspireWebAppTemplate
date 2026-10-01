using System.Security.Claims;
using AspireWebAppTemplate.Web.Abstractions;
using Microsoft.AspNetCore.Components.Authorization;

namespace AspireWebAppTemplate.Web.Services;

/// <summary>
/// Per-circuit effective-permission cache for the resource-based (<c>Module.Action</c>) authorization
/// model. Loads the authenticated user's effective permission keys once during circuit initialization
/// and provides synchronous in-memory lookups for navigation and page authorization checks.
/// </summary>
/// <remarks>
/// <para>
/// <b>Per-circuit caching strategy:</b> This service is registered as <b>scoped</b>, meaning each
/// Blazor Server SignalR circuit (user session) gets its own instance. Effective permissions (the
/// union of grants across all of the user's roles) are loaded a single time via
/// <see cref="InitializeAsync"/> and remain cached for the lifetime of the circuit. This eliminates
/// repeated API calls on every navigation event. Users must refresh or start a new session to pick
/// up permission changes made by an administrator.
/// </para>
/// <para>
/// <b>O(1) lookup rationale:</b> Permission keys are stored in a <see cref="HashSet{T}"/> with
/// <see cref="StringComparer.OrdinalIgnoreCase"/> to guarantee constant-time, case-insensitive
/// membership checks. This ensures <see cref="HasPermission"/> and <see cref="HasAnyPermissionInModule"/>
/// introduce no measurable latency during navigation, regardless of how many permissions are granted.
/// </para>
/// <para>
/// <b>Admin short-circuit:</b> When the current user holds the Admin role (<see cref="IsAdmin"/>), both
/// permission checks return <c>true</c> without consulting the cached set and without requiring the
/// cache to be loaded. Admins therefore bypass all permission filtering.
/// </para>
/// <para>
/// <b>Graceful degradation:</b> Unauthenticated users skip the API call entirely (empty cache). On API
/// failure or an unexpected error, the cache remains empty and a warning is logged; non-Admin checks
/// return <c>false</c>. <see cref="IsLoaded"/> is always set to <c>true</c> once initialization
/// completes (success or failure) so the UI does not remain stuck in a loading state.
/// </para>
/// </remarks>
public sealed class PermissionContext : IPermissionContext
{
    #region Constructor

    /// <summary>
    /// The HTTP client wrapper for calling the resource-based permission API endpoints.
    /// </summary>
    private readonly ApiPermissionService _apiService;

    /// <summary>
    /// The Blazor authentication state provider used to determine whether the current user is
    /// authenticated and to inspect the user's role claims.
    /// </summary>
    private readonly AuthenticationStateProvider _authStateProvider;

    /// <summary>
    /// Logger for recording initialization failures and diagnostic information.
    /// </summary>
    private readonly ILogger<PermissionContext> _logger;

    /// <summary>
    /// The role-claim value identifying an administrator. Admins bypass all permission checks.
    /// </summary>
    private const string AdminRoleName = "Admin";

    /// <summary>
    /// The set of effective permission keys granted to the current user, populated during
    /// initialization. Uses OrdinalIgnoreCase comparison for case-insensitive O(1) membership tests.
    /// </summary>
    private HashSet<string> _permissions = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Tracks whether the current user holds the Admin role, captured from the authentication state
    /// during <see cref="InitializeAsync"/>.
    /// </summary>
    private bool _isAdmin;

    /// <summary>
    /// Tracks whether <see cref="InitializeAsync"/> has completed (success or failure).
    /// When false, non-Admin permission checks return <c>false</c> to prevent unauthorized access
    /// before the cache is populated.
    /// </summary>
    private bool _isLoaded;

    /// <summary>
    /// Initializes a new instance of <see cref="PermissionContext"/>.
    /// </summary>
    /// <param name="apiService">
    /// The HTTP client wrapper for calling the resource-based permission API endpoints.
    /// </param>
    /// <param name="authStateProvider">
    /// The Blazor authentication state provider used to determine whether the current user is
    /// authenticated and to inspect the user's role claims.
    /// </param>
    /// <param name="logger">
    /// Logger for recording initialization failures and diagnostic information.
    /// </param>
    public PermissionContext(
        ApiPermissionService apiService,
        AuthenticationStateProvider authStateProvider,
        ILogger<PermissionContext> logger)
    {
        _apiService = apiService;
        _authStateProvider = authStateProvider;
        _logger = logger;
    }

    #endregion

    #region Properties

    /// <inheritdoc />
    public bool IsLoaded => _isLoaded;

    /// <inheritdoc />
    public bool IsAdmin => _isAdmin;

    #endregion

    #region Permission Checks

    /// <inheritdoc />
    /// <remarks>
    /// Evaluation order:
    /// 1. Admin — always return true (administrators bypass all permission checks)
    /// 2. Not loaded — return false for non-Admin users until the cache is populated
    /// 3. HashSet membership — O(1) case-insensitive lookup against the cached permission keys
    /// </remarks>
    public bool HasPermission(string permissionKey)
    {
        // Admin bypass: administrators are treated as holding every permission.
        if (_isAdmin)
            return true;

        // Deny non-Admin checks until the cache has been loaded to avoid granting
        // access before effective permissions are known.
        if (!_isLoaded)
            return false;

        // O(1) case-insensitive membership test against the cached effective permissions.
        return _permissions.Contains(permissionKey);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Evaluation order:
    /// 1. Admin — always return true (administrators bypass all permission checks)
    /// 2. Not loaded — return false for non-Admin users until the cache is populated
    /// 3. Prefix match — true if any cached key begins with <paramref name="module"/> + "." (case-insensitive)
    /// </remarks>
    public bool HasAnyPermissionInModule(string module)
    {
        // Admin bypass: administrators have access to every module.
        if (_isAdmin)
            return true;

        // Deny non-Admin checks until the cache has been loaded.
        if (!_isLoaded)
            return false;

        // A permission belongs to the module when its key starts with "{module}." —
        // the trailing dot prevents false matches on modules sharing a name prefix.
        var prefix = module + ".";
        return _permissions.Any(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Initialization

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Called once per circuit during initialization (typically from the root layout or auth state handler).
    /// </para>
    /// <para>
    /// If the user is unauthenticated, the method skips the API call entirely and leaves the cache
    /// empty — unauthenticated users hold no permissions.
    /// </para>
    /// <para>
    /// For authenticated users, the Admin role is captured from the authentication state's role claims
    /// (case-insensitive match on "Admin"). The effective permission keys are then loaded via a single
    /// API call. On API failure, the cache remains empty and a warning is logged; non-Admin checks
    /// return <c>false</c>. The <see cref="IsLoaded"/> property is always set to <c>true</c> once
    /// initialization completes so the UI does not remain in a perpetual loading state.
    /// </para>
    /// </remarks>
    public async Task InitializeAsync()
    {
        try
        {
            // Check authentication state — unauthenticated users hold no permissions,
            // so skip the API call and leave the cache empty.
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            if (authState.User.Identity?.IsAuthenticated != true)
            {
                return;
            }

            // Capture Admin membership from the user's role claims (case-insensitive).
            // Admins bypass permission checks, so this flag short-circuits all lookups.
            _isAdmin = authState.User.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Any(c => string.Equals(c.Value, AdminRoleName, StringComparison.OrdinalIgnoreCase));

            // Single API call to load the effective permission keys (union across all roles).
            var result = await _apiService.GetMyPermissionsAsync();

            if (result.Succeeded && result.Data is not null)
            {
                // Populate the HashSet with OrdinalIgnoreCase for case-insensitive O(1) lookups.
                _permissions = new HashSet<string>(result.Data, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                // API call returned a non-success result — keep the cache empty and log a warning.
                // Non-Admin permission checks will return false until the next circuit initialization.
                _logger.LogWarning(
                    "Failed to load effective permissions from API. Error: {Error}. " +
                    "Non-admin users will hold no permissions until the next circuit initialization.",
                    result.Error);
                _permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex)
        {
            // Network failure or unexpected error — keep the cache empty and log a warning.
            // Non-Admin permission checks return false; the user can refresh to retry.
            _logger.LogWarning(ex,
                "Exception occurred while loading effective permissions. " +
                "Non-admin users will hold no permissions until the next circuit initialization.");
            _permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            // Always mark as loaded (even on failure) so the UI transitions out of
            // the loading state and doesn't leave the user stuck on a loading skeleton.
            _isLoaded = true;
        }
    }

    #endregion
}
