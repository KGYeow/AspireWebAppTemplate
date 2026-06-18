// Feature: page-access-permissions, Property 9: NavMenu Filters Inaccessible Items
using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Web.Abstractions;
using AspireWebAppTemplate.Web.Components.Layout;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Moq;
using System.Reflection;
using System.Security.Claims;
using Gen = FsCheck.Fluent.Gen;
using Property = FsCheck.Property;

namespace AspireWebAppTemplate.Tests.PagePermissions.Properties;

/// <summary>
/// Property-based tests verifying that the NavMenu component correctly filters
/// navigation items based on PagePermissionContext state. Only Link items whose
/// Href causes CanAccess to return true (plus System_Page items) should be visible.
/// </summary>
/// <remarks>
/// <para>
/// <b>Property 9: NavMenu Filters Inaccessible Items</b> — For any set of NavItems of
/// type Link and any permission cache state, the NavMenu SHALL render only those Link items
/// whose Href causes PagePermissionContext.CanAccess to return true (plus System_Page items
/// which are always rendered).
/// </para>
/// <para>
/// <b>Validates: Requirements 7.1</b>
/// </para>
/// </remarks>
public class NavMenuPermissionPropertyTests
{
    /// <summary>
    /// System pages defined in NavMenu — these always bypass permission checks.
    /// </summary>
    private static readonly HashSet<string> SystemPages = new(StringComparer.OrdinalIgnoreCase)
    {
        "/Account/Login",
        "/Account/Register",
        "/Account/AccessDenied",
        "/Error",
        "/Account/ForgotPassword",
        "/Account/ResetPassword",
        "/Account/PerformLogin"
    };

    /// <summary>
    /// Determines whether a Link NavItem should be visible in the NavMenu given the permission state.
    /// This replicates the logic from NavMenu.ShouldShowItem to verify the expected contract.
    /// </summary>
    /// <remarks>
    /// Evaluation order matches NavMenu.razor.cs:
    /// 1. Null Href → always show (non-navigable)
    /// 2. Empty Href "" → treat as "/" (home page), check permissions
    /// 3. System_Page → always show
    /// 4. Permissions not loaded → hide
    /// 5. Otherwise → CanAccess check
    /// </remarks>
    private static bool ExpectedShouldShow(NavItem item, IPagePermissionContext context)
    {
        if (string.IsNullOrEmpty(item.Href))
        {
            // Empty string "" represents the Home page (root path "/")
            if (item.Href is not null && item.Href == "")
            {
                if (SystemPages.Contains("/"))
                    return true;
                if (!context.IsLoaded)
                    return false;
                return context.CanAccess("/");
            }
            // Null Href means truly non-navigable — always show
            return true;
        }

        var fullPath = "/" + item.Href;

        // System_Pages always bypass permission checks
        if (SystemPages.Contains(fullPath))
            return true;

        // If permissions haven't loaded yet, hide non-System_Page items
        if (!context.IsLoaded)
            return false;

        // Delegate to the permission context
        return context.CanAccess(fullPath);
    }

    /// <summary>
    /// Invokes the private ShouldShowItem method on the NavMenu component instance
    /// to verify the actual implementation matches expected behavior.
    /// </summary>
    private static bool InvokeShouldShowItem(NavMenu navMenu, NavItem item)
    {
        var method = typeof(NavMenu).GetMethod("ShouldShowItem", BindingFlags.NonPublic | BindingFlags.Instance);
        if (method is null)
            throw new InvalidOperationException("Could not find ShouldShowItem method on NavMenu");
        return (bool)method.Invoke(navMenu, [item])!;
    }

    /// <summary>
    /// Creates a NavMenu instance with a mocked IPagePermissionContext injected.
    /// Uses reflection to set the injected property since NavMenu is a Blazor component.
    /// </summary>
    private static NavMenu CreateNavMenuWithContext(IPagePermissionContext permissionContext)
    {
        var navMenu = new NavMenu();

        // Inject PagePermissionContext via the private property
        var contextProp = typeof(NavMenu).GetProperty("PagePermissionContext", BindingFlags.NonPublic | BindingFlags.Instance);
        if (contextProp is null)
            throw new InvalidOperationException("Could not find PagePermissionContext property on NavMenu");
        contextProp.SetValue(navMenu, permissionContext);

        // Inject a mock NavigationManager (required to avoid null reference in constructor)
        var navManagerProp = typeof(NavMenu).GetProperty("NavigationManager", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
        if (navManagerProp is not null)
        {
            var mockNavManager = new Mock<NavigationManager>();
            navManagerProp.SetValue(navMenu, mockNavManager.Object);
        }

        return navMenu;
    }

    /// <summary>
    /// Property: For any set of Link NavItems and any permission cache state (loaded with
    /// a random subset of pages being accessible), only Link items whose Href causes
    /// CanAccess to return true or whose Href is a System_Page are shown.
    /// <para><b>Validates: Requirements 7.1</b></para>
    /// </summary>
    [Property(MaxTest = 2)]
    public Property NavMenu_OnlyRendersAccessibleLinks_OrSystemPages()
    {
        // Generator for non-system page Href values (without leading "/", as NavItem stores them)
        var regularHrefGen = Gen.Elements(
            "counter", "weather", "admin/audit-log", "admin/user-management",
            "admin/role-management", "admin/page-permissions", "dashboard",
            "reports", "settings", "profile");

        // Generator for system page Href values (without leading "/")
        var systemHrefGen = Gen.Elements(
            "Account/Login", "Account/Register", "Account/AccessDenied",
            "Error", "Account/ForgotPassword", "Account/ResetPassword",
            "Account/PerformLogin");

        // Generator for a Link NavItem with either a regular or system page Href
        var linkNavItemGen = Gen.Frequency(
            (7, regularHrefGen.Select(href => new NavItem { Type = NavItemType.Link, Text = href, Href = href })),
            (3, systemHrefGen.Select(href => new NavItem { Type = NavItemType.Link, Text = href, Href = href })));

        // Generator for a set of 2-8 Link NavItems
        var navItemsGen = Gen.Choose(2, 8).SelectMany<int, List<NavItem>>(count =>
            Gen.ArrayOf(linkNavItemGen, count).Select(items => items.ToList()));

        // Generator for a random subset of regular pages that are "accessible" (permission granted)
        var accessiblePagesGen = Gen.SubListOf(new[]
        {
            "/counter", "/weather", "/admin/audit-log", "/admin/user-management",
            "/admin/role-management", "/admin/page-permissions", "/dashboard",
            "/reports", "/settings", "/profile"
        }).Select(pages => new HashSet<string>(pages, StringComparer.OrdinalIgnoreCase));

        // Combine generators
        var gen = navItemsGen.SelectMany<List<NavItem>, (List<NavItem> items, HashSet<string> accessible)>(items =>
            accessiblePagesGen.Select(accessible => (items, accessible)));

        return Prop.ForAll(Arb.From(gen),
            ((List<NavItem> items, HashSet<string> accessible) input) =>
        {
            // Arrange: Create a mock IPagePermissionContext that is loaded
            var mockContext = new Mock<IPagePermissionContext>();
            mockContext.Setup(c => c.IsLoaded).Returns(true);
            mockContext.Setup(c => c.CanAccess(It.IsAny<string>()))
                .Returns<string>(path => input.accessible.Contains(path));

            // Create a NavMenu instance with the mocked context
            var navMenu = CreateNavMenuWithContext(mockContext.Object);

            // Act & Assert: For each Link item, verify the filter outcome matches expectation
            var allCorrect = true;
            var failureDetails = "";

            foreach (var item in input.items)
            {
                var actual = InvokeShouldShowItem(navMenu, item);
                var expected = ExpectedShouldShow(item, mockContext.Object);

                if (actual != expected)
                {
                    allCorrect = false;
                    var fullPath = string.IsNullOrEmpty(item.Href) ? "(null/empty)" : "/" + item.Href;
                    failureDetails = $"Href='{item.Href}', FullPath='{fullPath}', " +
                                     $"Expected={expected}, Actual={actual}, " +
                                     $"IsSystemPage={SystemPages.Contains(fullPath)}, " +
                                     $"IsAccessible={input.accessible.Contains(fullPath)}";
                    break;
                }
            }

            return allCorrect
                .Label($"Items={input.items.Count}, Accessible={input.accessible.Count}, " +
                       $"Failure: {failureDetails}");
        });
    }

    // Feature: page-access-permissions, Property 10: Empty Groups Hidden

    /// <summary>
    /// Invokes the private HasVisibleChildren method on the NavMenu component instance
    /// to verify the actual implementation matches expected behavior for group visibility.
    /// </summary>
    private static bool InvokeHasVisibleChildren(NavMenu navMenu, NavItem group)
    {
        var method = typeof(NavMenu).GetMethod("HasVisibleChildren", BindingFlags.NonPublic | BindingFlags.Instance);
        if (method is null)
            throw new InvalidOperationException("Could not find HasVisibleChildren method on NavMenu");
        return (bool)method.Invoke(navMenu, [group])!;
    }

    /// <summary>
    /// Determines the expected visibility of a group nav item based on its children
    /// and the permission context. A group is visible if at least one child Link
    /// passes ShouldShowItem, or if a nested Group recursively has visible children.
    /// </summary>
    private static bool ExpectedHasVisibleChildren(NavItem group, IPagePermissionContext context)
    {
        if (group.Children is not { Count: > 0 })
            return false;

        foreach (var child in group.Children)
        {
            switch (child.Type)
            {
                case NavItemType.Link:
                    if (ExpectedShouldShow(child, context))
                        return true;
                    break;

                case NavItemType.Group:
                    if (ExpectedHasVisibleChildren(child, context))
                        return true;
                    break;

                // Headers and Dividers don't count as visible children
                case NavItemType.Header:
                case NavItemType.Divider:
                default:
                    break;
            }
        }

        return false;
    }

    /// <summary>
    /// Property 10: Empty Groups Hidden — For any NavItem of type Group, if all of its
    /// children (Link items) have Hrefs that cause CanAccess to return false AND those
    /// Hrefs are not System_Pages, then the group itself shall not be rendered.
    /// Conversely, groups with at least one visible child should be shown.
    /// <para><b>Validates: Requirements 7.2</b></para>
    /// </summary>
    [Property(MaxTest = 2)]
    public Property NavMenu_HidesGroups_WithZeroVisibleChildren()
    {
        // Generator for non-system page Href values (without leading "/")
        var regularHrefGen = Gen.Elements(
            "counter", "weather", "admin/audit-log", "admin/user-management",
            "admin/role-management", "admin/page-permissions", "dashboard",
            "reports", "settings", "profile");

        // Generator for system page Href values (without leading "/")
        var systemHrefGen = Gen.Elements(
            "Account/Login", "Account/Register", "Account/AccessDenied",
            "Error", "Account/ForgotPassword", "Account/ResetPassword",
            "Account/PerformLogin");

        // Generator for a Link child item (mix of regular and system pages)
        var linkChildGen = Gen.Frequency(
            (7, regularHrefGen.Select(href => new NavItem { Type = NavItemType.Link, Text = href, Href = href })),
            (3, systemHrefGen.Select(href => new NavItem { Type = NavItemType.Link, Text = href, Href = href })));

        // Generator for non-Link children (Headers and Dividers — they don't count as visible)
        var decorativeChildGen = Gen.Frequency(
            (1, Gen.Constant(new NavItem { Type = NavItemType.Header, Text = "Section" })),
            (1, Gen.Constant(new NavItem { Type = NavItemType.Divider })));

        // Generator for a child item (mix of Links and decorative items)
        var childItemGen = Gen.Frequency(
            (7, linkChildGen),
            (3, decorativeChildGen));

        // Generator for a group's children (1-6 items)
        var childrenGen = Gen.Choose(1, 6).SelectMany<int, List<NavItem>>(count =>
            Gen.ArrayOf(childItemGen, count).Select(items => items.ToList()));

        // Generator for a Group NavItem with random children
        var groupGen = childrenGen.Select(children => new NavItem
        {
            Type = NavItemType.Group,
            Text = "TestGroup",
            Icon = "material-symbols-rounded/folder",
            AuthorizedOnly = true,
            Children = children
        });

        // Generator for a random subset of regular pages that are "accessible"
        var accessiblePagesGen = Gen.SubListOf(new[]
        {
            "/counter", "/weather", "/admin/audit-log", "/admin/user-management",
            "/admin/role-management", "/admin/page-permissions", "/dashboard",
            "/reports", "/settings", "/profile"
        }).Select(pages => new HashSet<string>(pages, StringComparer.OrdinalIgnoreCase));

        // Combine generators
        var gen = groupGen.SelectMany<NavItem, (NavItem group, HashSet<string> accessible)>(group =>
            accessiblePagesGen.Select(accessible => (group, accessible)));

        return Prop.ForAll(Arb.From(gen),
            ((NavItem group, HashSet<string> accessible) input) =>
        {
            // Arrange: Create a mock IPagePermissionContext that is loaded
            var mockContext = new Mock<IPagePermissionContext>();
            mockContext.Setup(c => c.IsLoaded).Returns(true);
            mockContext.Setup(c => c.CanAccess(It.IsAny<string>()))
                .Returns<string>(path => input.accessible.Contains(path));

            // Create a NavMenu instance with the mocked context
            var navMenu = CreateNavMenuWithContext(mockContext.Object);

            // Act: Invoke HasVisibleChildren via reflection
            var actual = InvokeHasVisibleChildren(navMenu, input.group);

            // Assert: Compare against expected behavior
            var expected = ExpectedHasVisibleChildren(input.group, mockContext.Object);

            return (actual == expected)
                .Label($"Group children={input.group.Children?.Count ?? 0}, " +
                       $"Accessible={input.accessible.Count}, " +
                       $"Expected={expected}, Actual={actual}");
        });
    }

    /// <summary>
    /// Property 10 (supplemental): Groups with ALL non-system Link children denied
    /// and no system page children are always hidden — verifying the hiding condition directly.
    /// <para><b>Validates: Requirements 7.2</b></para>
    /// </summary>
    [Property(MaxTest = 2)]
    public Property NavMenu_AlwaysHidesGroup_WhenAllChildrenDenied()
    {
        // Generator for non-system page Href values only (without leading "/")
        var regularHrefGen = Gen.Elements(
            "counter", "weather", "admin/audit-log", "admin/user-management",
            "admin/role-management", "admin/page-permissions", "dashboard",
            "reports", "settings", "profile");

        // Generate only Link children with non-system Hrefs
        var linkChildGen = regularHrefGen.Select(href =>
            new NavItem { Type = NavItemType.Link, Text = href, Href = href });

        // Generate a group with 1-5 Link children (all non-system pages)
        var childrenGen = Gen.Choose(1, 5).SelectMany<int, List<NavItem>>(count =>
            Gen.ArrayOf(linkChildGen, count).Select(items => items.ToList()));

        var groupGen = childrenGen.Select(children => new NavItem
        {
            Type = NavItemType.Group,
            Text = "DeniedGroup",
            Icon = "material-symbols-rounded/block",
            AuthorizedOnly = true,
            Children = children
        });

        return Prop.ForAll(Arb.From(groupGen), (NavItem group) =>
        {
            // Arrange: Context is loaded but NO pages are accessible (empty set)
            var mockContext = new Mock<IPagePermissionContext>();
            mockContext.Setup(c => c.IsLoaded).Returns(true);
            mockContext.Setup(c => c.CanAccess(It.IsAny<string>())).Returns(false);

            var navMenu = CreateNavMenuWithContext(mockContext.Object);

            // Act: Group should be hidden because all children are denied
            var actual = InvokeHasVisibleChildren(navMenu, group);

            return (!actual)
                .Label($"Group should be hidden when all children denied. " +
                       $"Children={group.Children?.Count ?? 0}, Result={actual}");
        });
    }

    /// <summary>
    /// Property: When PagePermissionContext.IsLoaded is false, Link items that are NOT
    /// System_Pages should be hidden (ShouldShowItem returns false).
    /// <para><b>Validates: Requirements 7.1</b></para>
    /// </summary>
    [Property(MaxTest = 2)]
    public Property NavMenu_HidesNonSystemLinks_WhenNotLoaded()
    {
        // Generator for non-system page Href values
        var regularHrefGen = Gen.Elements(
            "counter", "weather", "admin/audit-log", "admin/user-management",
            "admin/role-management", "admin/page-permissions", "dashboard",
            "reports", "settings", "profile");

        // Generator for system page Href values
        var systemHrefGen = Gen.Elements(
            "Account/Login", "Account/Register", "Account/AccessDenied",
            "Error", "Account/ForgotPassword", "Account/ResetPassword",
            "Account/PerformLogin");

        // Generate a mix of regular and system Link items
        var linkNavItemGen = Gen.Frequency(
            (6, regularHrefGen.Select(href => new NavItem { Type = NavItemType.Link, Text = href, Href = href })),
            (4, systemHrefGen.Select(href => new NavItem { Type = NavItemType.Link, Text = href, Href = href })));

        var navItemsGen = Gen.Choose(2, 8).SelectMany<int, List<NavItem>>(count =>
            Gen.ArrayOf(linkNavItemGen, count).Select(items => items.ToList()));

        return Prop.ForAll(Arb.From(navItemsGen), (List<NavItem> items) =>
        {
            // Arrange: Context is NOT loaded (IsLoaded = false)
            var mockContext = new Mock<IPagePermissionContext>();
            mockContext.Setup(c => c.IsLoaded).Returns(false);
            // CanAccess should never be called when not loaded, but return false as default
            mockContext.Setup(c => c.CanAccess(It.IsAny<string>())).Returns(false);

            var navMenu = CreateNavMenuWithContext(mockContext.Object);

            // Act & Assert: System pages should still be shown; non-system pages should be hidden
            var allCorrect = true;
            var failureDetails = "";

            foreach (var item in items)
            {
                var actual = InvokeShouldShowItem(navMenu, item);
                var fullPath = string.IsNullOrEmpty(item.Href) ? "/" : "/" + item.Href;
                var isSystemPage = SystemPages.Contains(fullPath);

                // When not loaded: System_Pages → true, others → false
                var expected = isSystemPage;

                if (actual != expected)
                {
                    allCorrect = false;
                    failureDetails = $"Href='{item.Href}', FullPath='{fullPath}', " +
                                     $"IsSystemPage={isSystemPage}, Expected={expected}, Actual={actual}";
                    break;
                }
            }

            return allCorrect
                .Label($"Items={items.Count}, NotLoaded scenario, Failure: {failureDetails}");
        });
    }
}
