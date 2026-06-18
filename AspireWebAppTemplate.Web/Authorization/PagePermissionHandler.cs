using System.Reflection;
using AspireWebAppTemplate.Core.Common.Defaults;
using AspireWebAppTemplate.Web.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using RouteData = Microsoft.AspNetCore.Components.RouteData;

namespace AspireWebAppTemplate.Web.Authorization;

/// <summary>
/// Authorization handler that evaluates page-level access permissions for Blazor Server navigation.
/// Uses a four-step synchronous evaluation algorithm against cached permission data to ensure
/// zero-latency authorization decisions during page navigation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Evaluation order:</b>
/// <list type="number">
///   <item><description>Admin role → succeed immediately (Admin always has full access)</description></item>
///   <item><description>System_Page → succeed immediately (authentication/error pages are always accessible)</description></item>
///   <item><description>Cached permission check → succeed if granted, fail if denied</description></item>
///   <item><description>Path undetermined → succeed (avoid blocking non-page resources like static assets)</description></item>
/// </list>
/// </para>
/// <para>
/// This handler is invoked by <c>AuthorizeRouteView</c> during Blazor navigation. The authorization
/// resource is a <see cref="RouteData"/> object from which the page route template is extracted.
/// All checks are performed synchronously using the in-memory cached data from
/// <see cref="IPagePermissionContext"/>, satisfying the zero-latency navigation requirement.
/// </para>
/// </remarks>
public class PagePermissionHandler : AuthorizationHandler<PagePermissionRequirement>
{
    private readonly IPagePermissionContext _permissionContext;

    /// <summary>
    /// Initializes a new instance of <see cref="PagePermissionHandler"/>.
    /// </summary>
    /// <param name="permissionContext">
    /// The per-circuit permission cache providing synchronous O(1) page access lookups.
    /// </param>
    public PagePermissionHandler(IPagePermissionContext permissionContext)
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
    /// The <see cref="PagePermissionRequirement"/> triggering this evaluation.
    /// </param>
    /// <returns>A completed task (all checks are synchronous using cached data).</returns>
    /// <remarks>
    /// <para>
    /// The evaluation is performed entirely synchronously — no async I/O occurs here.
    /// The <see cref="IPagePermissionContext"/> was populated during circuit initialization
    /// and provides O(1) HashSet lookups for permission decisions.
    /// </para>
    /// <para>
    /// If the page path cannot be determined from the authorization resource (e.g., the resource
    /// is not a <see cref="RouteData"/> or the page type has no <c>@page</c> directive),
    /// the handler succeeds to avoid blocking non-page resources such as static assets or layout components.
    /// </para>
    /// </remarks>
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PagePermissionRequirement requirement)
    {
        // --- Step 1: Admin role check ---
        // Admin users always have full access to all pages regardless of permission records.
        // This is the first check to short-circuit as quickly as possible for administrators.
        if (context.User.IsInRole("Admin"))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Extract the page path from the authorization resource.
        // In Blazor Server, AuthorizeRouteView passes RouteData as the resource.
        var pagePath = ExtractPagePath(context.Resource);

        // --- Step 4 (early exit): Path undetermined ---
        // If we cannot determine the page path from the resource, succeed the requirement
        // to avoid blocking non-page resources (static assets, layout components, etc.).
        if (string.IsNullOrEmpty(pagePath))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // --- Step 2: System_Page check ---
        // System pages (Login, Register, AccessDenied, Error, etc.) are always accessible
        // regardless of permission state. These pages are essential for authentication flow
        // and error handling — blocking them would break the application.
        if (SystemPageDefaults.Paths.Contains(pagePath))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // --- Step 3: Cached permission check ---
        // Consult the per-circuit permission cache for an O(1) case-insensitive lookup.
        // If the cache indicates access is granted, succeed; otherwise fail the requirement
        // which causes Blazor to redirect to the AccessDenied view.
        if (_permissionContext.CanAccess(pagePath))
        {
            context.Succeed(requirement);
        }
        // If CanAccess returns false, we intentionally do NOT call context.Fail() here.
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
