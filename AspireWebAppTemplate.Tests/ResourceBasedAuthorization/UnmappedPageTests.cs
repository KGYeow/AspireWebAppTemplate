// Feature: resource-based-authorization, Property 7: Unmapped pages bypass module permission checks
using AspireWebAppTemplate.Domain.Constants;
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

namespace AspireWebAppTemplate.Tests.ResourceBasedAuthorization;

/// <summary>
/// Property-based tests verifying that <see cref="PageAccessAuthorizationHandler"/> grants access to pages
/// that are NOT present in its embedded admin page-to-module map, requiring only authentication and
/// no module permission (Requirement 6.8).
/// </summary>
/// <remarks>
/// <para>
/// <b>Property 7: Unmapped pages bypass module permission checks.</b> For any page path that is not
/// one of the six mapped admin pages and not a system page, the handler succeeds the authorization
/// requirement for an authenticated non-Admin user even when the permission cache grants no module
/// permission whatsoever. This proves that unmapped pages fall through to the authentication-only
/// grant rather than being gated by <see cref="IPermissionContext.HasAnyPermissionInModule(string)"/>.
/// </para>
/// <para>
/// <b>Validates: Requirements 6.8</b>
/// </para>
/// <para>
/// <b>Technique:</b> The handler extracts the page path from the <see cref="RouteData.PageType"/>'s
/// first <see cref="RouteAttribute"/>. Because route attributes cannot be synthesized at runtime, this
/// test defines a fixed pool of test component types each annotated with a known, unmapped, non-system
/// <c>[Route]</c> template, and generates over that pool. The mock <see cref="IPermissionContext"/> is
/// configured with <c>IsLoaded = true</c>, <c>IsAdmin = false</c>, and
/// <see cref="IPermissionContext.HasAnyPermissionInModule(string)"/> returning <c>false</c> for every
/// module, so a successful authorization can only come from the unmapped-page bypass.
/// </para>
/// </remarks>
public class UnmappedPageTests
{
    #region Test Page Components

    /// <summary>
    /// Test page at an unmapped, non-system path. Used to verify the authentication-only grant.
    /// </summary>
    [Route("/announcements")]
    private sealed class AnnouncementsPage : ComponentBase { }

    /// <summary>
    /// Test page at an unmapped, non-system path. Used to verify the authentication-only grant.
    /// </summary>
    [Route("/counter")]
    private sealed class CounterPage : ComponentBase { }

    /// <summary>
    /// Test page at an unmapped, non-system path. Used to verify the authentication-only grant.
    /// </summary>
    [Route("/weather")]
    private sealed class WeatherPage : ComponentBase { }

    /// <summary>
    /// Test page at an unmapped, non-system path. Used to verify the authentication-only grant.
    /// </summary>
    [Route("/dashboard")]
    private sealed class DashboardPage : ComponentBase { }

    /// <summary>
    /// Test page at an unmapped, non-system path. Used to verify the authentication-only grant.
    /// </summary>
    [Route("/reports")]
    private sealed class ReportsPage : ComponentBase { }

    /// <summary>
    /// Test page at an unmapped, non-system path (a multi-segment business route). Used to verify
    /// the authentication-only grant.
    /// </summary>
    [Route("/admin/reports/summary")]
    private sealed class AdminReportsSummaryPage : ComponentBase { }

    /// <summary>
    /// Test page at an unmapped, non-system path. Used to verify the authentication-only grant.
    /// </summary>
    [Route("/orders")]
    private sealed class OrdersPage : ComponentBase { }

    /// <summary>
    /// Test page at an unmapped, non-system path. Used to verify the authentication-only grant.
    /// </summary>
    [Route("/inventory/items")]
    private sealed class InventoryItemsPage : ComponentBase { }

    /// <summary>
    /// The pool of page component types whose routes are unmapped (not one of the six admin pages)
    /// and non-system (absent from <see cref="SystemPageDefaults.Paths"/>). The property generates
    /// over this pool.
    /// </summary>
    private static readonly Type[] UnmappedPageTypes =
    [
        typeof(AnnouncementsPage),
        typeof(CounterPage),
        typeof(WeatherPage),
        typeof(DashboardPage),
        typeof(ReportsPage),
        typeof(AdminReportsSummaryPage),
        typeof(OrdersPage),
        typeof(InventoryItemsPage)
    ];

    #endregion

    #region Helpers

    /// <summary>
    /// Creates an authenticated, non-Admin <see cref="ClaimsPrincipal"/> holding a single ordinary role.
    /// </summary>
    /// <returns>A claims principal representing an authenticated non-administrator user.</returns>
    private static ClaimsPrincipal CreateNonAdminUser()
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim(ClaimTypes.Name, "regularuser"));
        identity.AddClaim(new Claim(ClaimTypes.Role, "User"));
        return new ClaimsPrincipal(identity);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Property: For any unmapped, non-system page path, the handler grants access to an authenticated
    /// non-Admin user even though the permission cache reports no module permission. This confirms the
    /// unmapped-page bypass (authentication only) rather than a module-gated grant.
    /// <para><b>Validates: Requirements 6.8</b></para>
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UnmappedPages_GrantAccess_WithAuthenticationOnly()
    {
        // Generate over the fixed pool of unmapped, non-system page component types.
        var pageTypeGen = Gen.Elements(UnmappedPageTypes);

        return Prop.ForAll(Arb.From(pageTypeGen), (Type pageType) =>
        {
            // Arrange: a non-Admin, fully-loaded cache that grants NO module permission at all.
            // If the handler relied on module permissions, authorization would fail; a success can
            // only come from the unmapped-page bypass.
            var mockPermissionContext = new Mock<IPermissionContext>(MockBehavior.Strict);
            mockPermissionContext.Setup(c => c.IsAdmin).Returns(false);
            mockPermissionContext.Setup(c => c.IsLoaded).Returns(true);
            mockPermissionContext
                .Setup(c => c.HasAnyPermissionInModule(It.IsAny<string>()))
                .Returns(false);

            var handler = new PageAccessAuthorizationHandler(mockPermissionContext.Object);
            var user = CreateNonAdminUser();
            var requirements = new[] { new PageAccessRequirement() };

            var routeData = new RouteData(pageType, new Dictionary<string, object?>());
            var authContext = new AuthorizationHandlerContext(requirements, user, routeData);

            // Act
            ((IAuthorizationHandler)handler).HandleAsync(authContext).GetAwaiter().GetResult();

            // Assert: the requirement succeeded (access granted) for the unmapped page.
            var succeeded = authContext.HasSucceeded;

            // And it was granted WITHOUT relying on a module permission: HasAnyPermissionInModule
            // returned false for every call it may have made (the handler does not need it for
            // unmapped paths — it returns false here precisely to prove the bypass).
            return succeeded
                .Label($"PageType='{pageType.Name}', HasSucceeded={succeeded} " +
                       "(expected success via unmapped-page bypass with no module permission)");
        });
    }

    #endregion
}
