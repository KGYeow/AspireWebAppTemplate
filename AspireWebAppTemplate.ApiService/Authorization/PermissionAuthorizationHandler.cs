using System.Security.Claims;
using AspireWebAppTemplate.Application.Features.Permissions;
using Microsoft.AspNetCore.Authorization;

namespace AspireWebAppTemplate.ApiService.Authorization;

/// <summary>
/// Evaluates a <see cref="PermissionRequirement"/> against the authenticated user's effective
/// permissions. The user is granted access when they hold the Admin role (implicit full access)
/// or when their effective permission set contains the requirement's permission key
/// (case-insensitive).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Admin bypass:</strong> If the user is in the <c>Admin</c> role (case-insensitive), the
/// requirement is satisfied immediately without any database query — the Admin role implicitly
/// holds every permission regardless of stored role-permission records.
/// </para>
/// <para>
/// <strong>Role-resolution approach (names, not IDs):</strong> In this application the API receives
/// user identity from the Web project via <c>X-User-*</c> headers, which the API's
/// <c>InternalAuthenticationHandler</c> turns into a <see cref="ClaimsPrincipal"/>. The forwarded
/// role claims carry role <em>names</em>, not role IDs. Rather than re-derive role IDs from name
/// claims here (which would duplicate Identity lookups and risk drift), this handler resolves the
/// user's effective permissions through <see cref="IPermissionService.GetMyPermissionsAsync"/>,
/// keyed off the authenticated user's <see cref="ClaimTypes.NameIdentifier"/> claim. That method
/// already performs the role name → role ID resolution and the role-grant union internally,
/// keeping permission resolution consistent with the rest of the application (e.g. the
/// <c>my-permissions</c> endpoint and the Web <c>PermissionContext</c>).
/// </para>
/// <para>
/// <strong>Per-request caching:</strong> this handler is registered as a scoped service because it
/// depends on the scoped <see cref="IPermissionService"/> (and, transitively, the scoped
/// <c>ApplicationDbContext</c>). To avoid re-querying the permission store when several permission
/// checks occur within one HTTP request, the resolved permission set is cached in
/// <see cref="HttpContext.Items"/> under a per-request key. Subsequent checks in the same request
/// read the cached set. <see cref="IHttpContextAccessor"/> provides the current request's context.
/// </para>
/// <para>
/// <strong>Fail closed:</strong> If no HTTP context is available, the user is not authenticated,
/// the user has no <see cref="ClaimTypes.NameIdentifier"/> claim, or loading the permission set
/// throws, the handler does not call <c>Succeed</c> — leaving the requirement unsatisfied so the
/// authorization pipeline denies access (401/403). Load failures are logged at Error level.
/// </para>
/// </remarks>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    #region Constructor

    /// <summary>
    /// The role name that grants implicit full access to every permission. Compared
    /// case-insensitively against the user's role membership.
    /// </summary>
    private const string AdminRoleName = "Admin";

    /// <summary>
    /// The <see cref="HttpContext.Items"/> key under which the resolved per-request permission set
    /// is cached, so repeated permission checks within one request reuse a single load.
    /// </summary>
    private const string PermissionCacheKey = "__EffectivePermissions";

    /// <summary>
    /// Provides access to the current HTTP request so the resolved permission set can be cached in
    /// <see cref="HttpContext.Items"/> for the lifetime of the request.
    /// </summary>
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// The permission service used to resolve the authenticated user's effective permission keys.
    /// </summary>
    private readonly IPermissionService _permissionService;

    /// <summary>
    /// The logger used to record permission-load failures (fail-closed scenarios) at Error level.
    /// </summary>
    private readonly ILogger<PermissionAuthorizationHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionAuthorizationHandler"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">Accessor for the current HTTP request used for per-request caching.</param>
    /// <param name="permissionService">The service that resolves the user's effective permissions.</param>
    /// <param name="logger">The logger for recording fail-closed permission-load failures.</param>
    public PermissionAuthorizationHandler(
        IHttpContextAccessor httpContextAccessor,
        IPermissionService permissionService,
        ILogger<PermissionAuthorizationHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _permissionService = permissionService;
        _logger = logger;
    }

    #endregion

    #region Requirement Evaluation

    /// <summary>
    /// Evaluates whether the current user satisfies the supplied <see cref="PermissionRequirement"/>.
    /// Grants access on Admin role membership (no database query) or when the user's effective
    /// permission set contains the required key (case-insensitive); otherwise leaves the
    /// requirement unsatisfied (fail closed).
    /// </summary>
    /// <param name="context">The authorization context carrying the user's <see cref="ClaimsPrincipal"/>.</param>
    /// <param name="requirement">The permission requirement being evaluated.</param>
    /// <returns>A task representing the asynchronous evaluation.</returns>
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var user = context.User;

        // Fail closed: an unauthenticated user can never satisfy a permission requirement.
        if (user?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        // Admin bypass: the Admin role implicitly holds every permission. Succeed immediately
        // without touching the database.
        if (user.IsInRole(AdminRoleName))
        {
            context.Succeed(requirement);
            return;
        }

        // Resolve the authenticated user's ID. Without it the user's permissions cannot be loaded,
        // so fail closed.
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        // Fail closed: a non-Admin user with no role claims has no granted permissions.
        if (!user.FindAll(ClaimTypes.Role).Any())
        {
            return;
        }

        // Resolve the effective permission set (per-request cached). A null result indicates a
        // load failure that was already logged; fail closed in that case.
        var permissions = await GetEffectivePermissionsAsync(userId);
        if (permissions is null)
        {
            return;
        }

        // Succeed only when the required key is present (case-insensitive membership).
        if (permissions.Contains(requirement.PermissionKey))
        {
            context.Succeed(requirement);
        }

        // Intentionally no context.Fail(): leaving the requirement unsatisfied denies access while
        // allowing other handlers (if any) a chance to satisfy it, per ASP.NET Core convention.
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Resolves the authenticated user's effective permission keys, caching the result in
    /// <see cref="HttpContext.Items"/> so repeated checks within the same request do not re-query.
    /// Returns <c>null</c> when the permission load fails (fail-closed signal); the failure is
    /// logged at Error level.
    /// </summary>
    /// <param name="userId">The authenticated user's unique identifier.</param>
    /// <returns>
    /// A case-insensitive set of the user's effective permission keys, or <c>null</c> if loading
    /// the permissions threw.
    /// </returns>
    private async Task<HashSet<string>?> GetEffectivePermissionsAsync(string userId)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        // Reuse the resolved set if a prior check in this request already cached it.
        if (httpContext is not null
            && httpContext.Items.TryGetValue(PermissionCacheKey, out var cached)
            && cached is HashSet<string> cachedSet)
        {
            return cachedSet;
        }

        try
        {
            var keys = await _permissionService.GetMyPermissionsAsync(userId);
            var set = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);

            // Cache for the remainder of the request so subsequent permission checks are free.
            if (httpContext is not null)
            {
                httpContext.Items[PermissionCacheKey] = set;
            }

            return set;
        }
        catch (Exception ex)
        {
            // Fail closed on any permission-load error: log and signal denial to the caller.
            _logger.LogError(
                ex,
                "Failed to load effective permissions for user {UserId}; denying access (fail closed).",
                userId);
            return null;
        }
    }

    #endregion
}
