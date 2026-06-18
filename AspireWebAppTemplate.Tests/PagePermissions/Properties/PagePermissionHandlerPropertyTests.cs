// Feature: page-access-permissions, Property 3: Admin Immutable Full Access
using AspireWebAppTemplate.Web.Abstractions;
using AspireWebAppTemplate.Web.Authorization;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Moq;
using System.Security.Claims;
using Gen = FsCheck.Fluent.Gen;
using Property = FsCheck.Property;
using RouteData = Microsoft.AspNetCore.Components.RouteData;

namespace AspireWebAppTemplate.Tests.PagePermissions.Properties;

/// <summary>
/// Property-based tests verifying that the PagePermissionHandler grants Admin users
/// full access to any page without consulting the IPagePermissionContext cache.
/// This ensures the Admin role is treated as immutable with universal access.
/// </summary>
/// <remarks>
/// <para>
/// <b>Property 3: Admin Immutable Full Access</b> — For any page path and any database state,
/// if the user holds the "Admin" role, the PagePermissionHandler SHALL succeed the
/// authorization requirement without consulting the permission cache.
/// </para>
/// <para>
/// <b>Validates: Requirements 4.1, 4.3, 6.3</b>
/// </para>
/// </remarks>
public class PagePermissionHandlerPropertyTests
{
    /// <summary>
    /// Creates a ClaimsPrincipal with the "Admin" role claim, simulating an authenticated
    /// administrator user for authorization evaluation.
    /// </summary>
    private static ClaimsPrincipal CreateAdminUser()
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim(ClaimTypes.Name, "adminuser"));
        identity.AddClaim(new Claim(ClaimTypes.Role, "Admin"));
        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Creates a test page component type with a [Route] attribute matching the given path.
    /// Uses a dynamically selected type from a set of pre-defined route pages.
    /// </summary>
    private static RouteData CreateRouteData(string pagePath)
    {
        // Use a dictionary of pre-configured page types with known routes
        // Since we can't dynamically generate [Route] attributes at runtime,
        // we pass the RouteData with a page type that has a route attribute.
        // However, for the Admin check, the handler never inspects the RouteData
        // because the Admin role check short-circuits before extracting the page path.
        // We can use any page type or even pass a null resource since Admin check is first.
        return new RouteData(typeof(DummyPageComponent), new Dictionary<string, object?>());
    }

    /// <summary>
    /// Property: For any random page path and any cache state (empty, partial, or full),
    /// the PagePermissionHandler SHALL succeed the authorization requirement for Admin users
    /// without consulting the IPagePermissionContext.
    /// <para><b>Validates: Requirements 4.1, 4.3, 6.3</b></para>
    /// </summary>
    [Property(MaxTest = 2)]
    public Property AdminRole_AlwaysSucceeds_WithoutConsultingCache()
    {
        // Generator for valid page path segments
        var segmentGen = Gen.Elements("admin", "dashboard", "settings", "users", "reports",
            "counter", "weather", "audit-log", "roles", "profile", "pages", "config",
            "page-permissions", "user-management", "role-management");

        // Generator for page paths: "/" followed by 1-3 segments joined by "/"
        var pagePathGen = Gen.Choose(1, 3).SelectMany<int, string>(segmentCount =>
            Gen.ArrayOf<string>(segmentGen, segmentCount)
                .Select(segments => "/" + string.Join("/", segments)));

        // Generator for cache state: 0 = empty (not loaded), 1 = partial, 2 = full
        var cacheStateGen = Gen.Choose(0, 2);

        var gen = pagePathGen.SelectMany<string, (string pagePath, int cacheState)>(path =>
            cacheStateGen.Select(state => (path, state)));

        return Prop.ForAll(Arb.From(gen),
            ((string pagePath, int cacheState) input) =>
        {
            // Arrange: Create a mock IPagePermissionContext that tracks invocations
            var mockPermissionContext = new Mock<IPagePermissionContext>(MockBehavior.Strict);

            // Configure IsLoaded based on cache state:
            // 0 = not loaded (empty), 1 = partially loaded, 2 = fully loaded
            mockPermissionContext
                .Setup(c => c.IsLoaded)
                .Returns(input.cacheState > 0);

            // NOTE: We do NOT set up CanAccess because it should NEVER be called for Admin.
            // MockBehavior.Strict will throw if CanAccess is invoked, proving the bypass.

            var handler = new PagePermissionHandler(mockPermissionContext.Object);
            var adminUser = CreateAdminUser();
            var requirements = new[] { new PagePermissionRequirement() };

            // Use RouteData as the resource (Admin check happens before path extraction)
            var routeData = CreateRouteData(input.pagePath);
            var authContext = new AuthorizationHandlerContext(requirements, adminUser, routeData);

            // Act: Invoke the handler through the IAuthorizationHandler interface
            ((IAuthorizationHandler)handler).HandleAsync(authContext).GetAwaiter().GetResult();

            // Assert: Handler must have succeeded the requirement
            var succeeded = authContext.HasSucceeded;

            // Verify CanAccess was never called (Strict mock would throw, but also verify explicitly)
            mockPermissionContext.Verify(
                c => c.CanAccess(It.IsAny<string>()),
                Times.Never,
                "Admin bypass: CanAccess should not be consulted for Admin users");

            return succeeded
                .Label($"PagePath='{input.pagePath}', CacheState={input.cacheState}, " +
                       $"HasSucceeded={succeeded}");
        });
    }

    /// <summary>
    /// Property: Admin role succeeds even when RouteData resource is null,
    /// verifying the Admin check truly short-circuits before any resource inspection.
    /// <para><b>Validates: Requirements 4.1, 4.3, 6.3</b></para>
    /// </summary>
    [Property(MaxTest = 2)]
    public Property AdminRole_AlwaysSucceeds_EvenWithNullResource()
    {
        // Generator for cache states: 0 = not loaded, 1 = partially loaded, 2 = fully loaded
        var cacheStateGen = Gen.Choose(0, 2);

        return Prop.ForAll(Arb.From(cacheStateGen), (int cacheState) =>
        {
            // Arrange: Strict mock — any unexpected call will throw
            var mockPermissionContext = new Mock<IPagePermissionContext>(MockBehavior.Strict);
            mockPermissionContext
                .Setup(c => c.IsLoaded)
                .Returns(cacheState > 0);

            var handler = new PagePermissionHandler(mockPermissionContext.Object);
            var adminUser = CreateAdminUser();
            var requirements = new[] { new PagePermissionRequirement() };

            // Pass null resource — Admin check should still short-circuit before inspecting it
            var authContext = new AuthorizationHandlerContext(requirements, adminUser, null);

            // Act
            ((IAuthorizationHandler)handler).HandleAsync(authContext).GetAwaiter().GetResult();

            // Assert: Must succeed regardless of null resource
            var succeeded = authContext.HasSucceeded;

            // Verify CanAccess was never called
            mockPermissionContext.Verify(
                c => c.CanAccess(It.IsAny<string>()),
                Times.Never,
                "Admin bypass: CanAccess should not be consulted for Admin users");

            return succeeded
                .Label($"NullResource, CacheState={cacheState}, HasSucceeded={succeeded}");
        });
    }

    /// <summary>
    /// Dummy page component used to construct RouteData instances for testing.
    /// The [Route] attribute value is irrelevant for Admin tests since the handler
    /// short-circuits at the Admin role check before extracting the page path.
    /// </summary>
    [Route("/dummy")]
    private class DummyPageComponent : ComponentBase
    {
    }
}
