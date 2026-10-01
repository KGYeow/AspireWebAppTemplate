using System.Reflection;
using AspireWebAppTemplate.Domain.Constants;
using AspireWebAppTemplate.Web.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using RouteData = Microsoft.AspNetCore.Components.RouteData;

namespace AspireWebAppTemplate.Web.Authorization;

/// <summary>
/// Authorization handler that evaluates page-level access permissions for Blazor Server navigation
/// under the resource-based (<c>Module.Action</c>) permission model. Resolves the module that gates
/// the requested admin page and grants access when the user holds any permission within that module,
/// using cached permission data for zero-latency authorization decisions during page navigation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Evaluation order:</b>
/// <list type="number">
///   <item><description>Admin role → succeed immediately (Admin always has full access)</description></item>
///   <item><description>Path undetermined → succeed (avoid blocking non-page resources like static assets)</description></item>
///   <item><description>System_Page → succeed immediately (authentication/error pages are always accessible)</description></item>
///   <item><description>Cache not yet loaded → succeed (avoid a redirect loop during circuit startup)</description></item>
///   <item><description>Module lookup → unmapped page succeeds (authentication only); a mapped page succeeds
///     only when the user holds any permission in that module</description></item>
/// </list>
/// </para>
/// <para>
/// This handler is invoked by <c>AuthorizeRouteView</c> during Blazor navigation. The authorization
/// resource is a <see cref="RouteData"/> object from which the page route template is extracted.
/// All checks are performed synchronously using the in-memory cached data from
/// <see cref="IPermissionContext"/>, satisfying the zero-latency navigation requirement.
/// </para>
/// </remarks>
public class PageAccessAuthorizationHandler : AuthorizationHandler<PageAccessRequirement>
{
    /// <summary>
    /// Static page-path to module mapping used to gate admin pages. This mirrors the canonical
    /// mapping exposed by the server-side permission service (<c>GetPageModuleMappingsAsync</c>).
    /// </summary>
    /// <remarks>
    /// The mapping is embedded here rather than fetched from the API because the authorization
    /// handler must run synchronously and with zero latency during navigation — an async round-trip
    /// is not acceptable in this hot path. The lookup is case-insensitive to match page-path
    /// comparison semantics used elsewhere in the permission system. Keep this table in sync with
    /// <c>PermissionService.PageModuleMappings</c>; any page absent from this map requires only
    /// authentication (see Req 6.8).
    /// </remarks>
    private static readonly IReadOnlyDictionary<string, string> PageModuleMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["/admin/user-management"] = "Users",
            ["/admin/role-management"] = "Roles",
            ["/admin/audit-log"] = "AuditLog",
            ["/admin/permission-management"] = "Permissions",
            ["/admin/announcements"] = "Announcements",
            ["/admin/email-templates"] = "EmailTemplates"
        };

    /// <summary>
    /// The per-circuit effective-permission cache providing synchronous module-membership lookups.
    /// </summary>
    private readonly IPermissionContext _permissionContext;

    /// <summary>
    /// Initializes a new instance of <see cref="PageAccessAuthorizationHandler"/>.
    /// </summary>
    /// <param name="permissionContext">
    /// The per-circuit permission cache providing synchronous module-membership and Admin lookups.
    /// </param>
    public PageAccessAuthorizationHandler(IPermissionContext permissionContext)
    {
        _permissionContext = permissionContext;
    }

    /// <summary>
    /// Evaluates whether the current user is authorized to access the requested page route.
    /// </summary>
    /// <param name="context">
    /// The authorization handler context containing the user's claims principal and the resource being accessed.
    /// </param>
    /// <param name="requirement">
    /// The <see cref="PageAccessRequirement"/> triggering this evaluation.
    /// </param>
    /// <returns>A completed task (all checks are synchronous using cached data).</returns>
    /// <remarks>
    /// <para>
    /// The evaluation is performed entirely synchronously — no async I/O occurs here.
    /// The <see cref="IPermissionContext"/> was populated during circuit initialization
    /// and provides O(1) lookups for permission decisions.
    /// </para>
    /// <para>
    /// If the page path cannot be determined from the authorization resource (e.g., the resource
    /// is not a <see cref="RouteData"/> or the page type has no <c>@page</c> directive),
    /// the handler succeeds to avoid blocking non-page resources such as static assets or layout components.
    /// </para>
    /// </remarks>
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PageAccessRequirement requirement)
    {
        // --- Step 1: Admin role check ---
        // Admin users always have full access to all pages regardless of permission records.
        // This is the first check to short-circuit as quickly as possible for administrators.
        if (_permissionContext.IsAdmin)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Extract the page path from the authorization resource.
        // In Blazor Server, AuthorizeRouteView passes RouteData as the resource.
        var pagePath = ExtractPagePath(context.Resource);

        // --- Step 2 (early exit): Path undetermined ---
        // If we cannot determine the page path from the resource, succeed the requirement
        // to avoid blocking non-page resources (static assets, layout components, etc.).
        if (string.IsNullOrEmpty(pagePath))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // --- Step 3: System_Page check ---
        // System pages (Login, Register, AccessDenied, Error, etc.) are always accessible
        // regardless of permission state. These pages are essential for authentication flow
        // and error handling — blocking them would break the application.
        if (SystemPageDefaults.Paths.Contains(pagePath))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // --- Step 4: Cache not yet loaded check ---
        // If the permission cache has not completed initialization (e.g., during circuit startup
        // when the root layout's initialization hasn't finished yet), succeed the requirement to
        // avoid blocking authenticated users before permissions are available. The NavMenu will
        // show a loading skeleton until the cache is populated, providing visual feedback.
        // Denying here would cause a redirect loop: user is authenticated but gets sent to
        // AccessDenied, which redirects back, creating a broken experience on page refresh.
        if (!_permissionContext.IsLoaded)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // --- Step 5: Module-based permission check ---
        // Resolve the module that gates this page. Pages that are not in the admin page-to-module
        // mapping require only authentication (Req 6.8), so succeed for any unmapped path.
        if (!PageModuleMap.TryGetValue(pagePath, out var module))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // The page is mapped to a module: grant access only when the user holds at least one
        // permission within that module (Req 6.6, 7.3).
        if (_permissionContext.HasAnyPermissionInModule(module))
        {
            context.Succeed(requirement);
        }
        // If the user holds no permission in the module, we intentionally do NOT call context.Fail().
        // Not calling Succeed() leaves the requirement unsatisfied, which triggers the
        // NotAuthorized template in AuthorizeRouteView (redirect to AccessDenied).
        // This follows the ASP.NET Core convention: handlers that cannot satisfy a requirement
        // should simply not call Succeed(), allowing other handlers to potentially satisfy it.

        return Task.CompletedTask;
    }

    /// <summary>
    /// Extracts the page route path from the authorization resource.
    /// </summary>
    /// <param name="resource">
    /// The authorization resource, expected to be a <see cref="RouteData"/> in Blazor Server.
    /// </param>
    /// <returns>
    /// The page route path (e.g., "/admin/audit-log") if it can be determined;
    /// <c>null</c> if the resource is not a <see cref="RouteData"/> or the page type
    /// has no <see cref="RouteAttribute"/>.
    /// </returns>
    /// <remarks>
    /// The page route template is extracted from the <see cref="RouteAttribute"/> applied
    /// to the page component type (generated from the <c>@page</c> directive in Razor files).
    /// Path comparison uses case-insensitive ordinal matching (OrdinalIgnoreCase) as required
    /// by the permission system.
    /// </remarks>
    private static string? ExtractPagePath(object? resource)
    {
        // AuthorizeRouteView passes Microsoft.AspNetCore.Components.RouteData as the resource
        if (resource is not RouteData routeData)
            return null;

        // The page type (component) has a [Route("...")] attribute generated from @page directive.
        // Extract the first RouteAttribute's Template as the canonical page path.
        var routeAttribute = routeData.PageType.GetCustomAttribute<RouteAttribute>();

        if (routeAttribute is null)
            return null;

        var template = routeAttribute.Template;

        // Ensure the path starts with "/" for consistent comparison.
        // Blazor @page directives typically include the leading slash, but normalize just in case.
        if (!string.IsNullOrEmpty(template) && !template.StartsWith('/'))
        {
            template = "/" + template;
        }

        return template;
    }
}
