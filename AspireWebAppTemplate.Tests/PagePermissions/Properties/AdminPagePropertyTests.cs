// Feature: page-access-permissions, Property 11: System Pages Excluded From Admin Matrix
using AspireWebAppTemplate.Core.Application.Abstractions;
using AspireWebAppTemplate.Core.Common;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using System.Reflection;
using Gen = FsCheck.Fluent.Gen;
using Property = FsCheck.Property;
using AdminPagePermissions = AspireWebAppTemplate.Web.Components.Pages.Admin.PagePermissions.PagePermissions;

namespace AspireWebAppTemplate.Tests.PagePermissions.Properties;

/// <summary>
/// Property-based tests verifying that the admin page permission matrix excludes
/// all System_Page paths from the page rows. System pages are always accessible
/// and should never appear as configurable rows in the matrix.
/// </summary>
/// <remarks>
/// <para>
/// <b>Property 11: System Pages Excluded From Admin Matrix</b> — For any page path
/// that matches a System_Page, the admin permission matrix SHALL NOT include that
/// path as a row.
/// </para>
/// <para>
/// <b>Validates: Requirements 8.10</b>
/// </para>
/// </remarks>
public class AdminPagePropertyTests
{
    /// <summary>
    /// The set of System_Pages that must be excluded from the permission matrix.
    /// These are always accessible regardless of permissions.
    /// </summary>
    private static readonly string[] SystemPagePaths =
    [
        "/Account/Login",
        "/Account/Register",
        "/Account/AccessDenied",
        "/Error",
        "/Account/ForgotPassword",
        "/Account/ResetPassword",
        "/Account/PerformLogin"
    ];

    /// <summary>
    /// System page Href values as they would appear in NavItem (without leading "/").
    /// </summary>
    private static readonly string[] SystemPageHrefs =
    [
        "Account/Login",
        "Account/Register",
        "Account/AccessDenied",
        "Error",
        "Account/ForgotPassword",
        "Account/ResetPassword",
        "Account/PerformLogin"
    ];

    /// <summary>
    /// Regular (non-system) page Href values for generating mixed navigation structures.
    /// </summary>
    private static readonly string[] RegularPageHrefs =
    [
        "counter",
        "weather",
        "admin/audit-log",
        "admin/user-management",
        "admin/role-management",
        "admin/page-permissions",
        "dashboard",
        "reports",
        "settings",
        "profile"
    ];

    /// <summary>
    /// Invokes the private ExtractPageRows method on the PagePermissions component
    /// via reflection to test the actual matrix page extraction logic.
    /// </summary>
    private static List<AdminPagePermissions.PageRow> InvokeExtractPageRows(AdminPagePermissions page)
    {
        var method = typeof(AdminPagePermissions).GetMethod(
            "ExtractPageRows",
            BindingFlags.NonPublic | BindingFlags.Instance);

        if (method is null)
            throw new InvalidOperationException("Could not find ExtractPageRows method on PagePermissions");

        return (List<AdminPagePermissions.PageRow>)method.Invoke(page, [])!;
    }

    /// <summary>
    /// Creates a PagePermissions component instance with a mocked INavigationProvider injected.
    /// Uses reflection to set the injected property since PagePermissions is a Blazor component.
    /// </summary>
    private static AdminPagePermissions CreatePagePermissionsWithProvider(INavigationProvider navigationProvider)
    {
        var page = new AdminPagePermissions();

        // Inject NavigationProvider via the private property
        var navProviderProp = typeof(AdminPagePermissions).GetProperty(
            "NavigationProvider",
            BindingFlags.NonPublic | BindingFlags.Instance);

        if (navProviderProp is null)
            throw new InvalidOperationException("Could not find NavigationProvider property on PagePermissions");

        navProviderProp.SetValue(page, navigationProvider);

        return page;
    }

    /// <summary>
    /// Property: For any navigation structure containing a mix of System_Page and regular
    /// Link NavItems (including items nested inside Group containers), the ExtractPageRows
    /// method SHALL NOT include any System_Page path in the returned page rows.
    /// <para><b>Validates: Requirements 8.10</b></para>
    /// </summary>
    [Property(MaxTest = 2)]
    public Property ExtractPageRows_ExcludesAllSystemPages_FromMatrix()
    {
        // Generator for a Link NavItem with a system page Href
        var systemLinkGen = Gen.Elements(SystemPageHrefs)
            .Select(href => new NavItem { Type = NavItemType.Link, Text = $"System: {href}", Href = href });

        // Generator for a Link NavItem with a regular page Href
        var regularLinkGen = Gen.Elements(RegularPageHrefs)
            .Select(href => new NavItem { Type = NavItemType.Link, Text = $"Page: {href}", Href = href });

        // Generator for a mixed Link NavItem (both system and regular pages)
        var linkGen = Gen.Frequency(
            (4, systemLinkGen),
            (6, regularLinkGen));

        // Generator for a Group NavItem that contains child Link items (including system pages)
        var groupGen = Gen.Choose(1, 4).SelectMany<int, NavItem>(childCount =>
            Gen.ArrayOf(linkGen, childCount).Select(children => new NavItem
            {
                Type = NavItemType.Group,
                Text = "Test Group",
                Children = children.ToList()
            }));

        // Generator for top-level NavItems: mix of links, groups, headers, and dividers
        var topLevelItemGen = Gen.Frequency(
            (4, linkGen),
            (3, groupGen),
            (1, Gen.Constant(new NavItem { Type = NavItemType.Header, Text = "Section" })),
            (1, Gen.Constant(new NavItem { Type = NavItemType.Divider })));

        // Generate a navigation structure with 3-10 top-level items
        var navItemsGen = Gen.Choose(3, 10).SelectMany<int, List<NavItem>>(count =>
            Gen.ArrayOf(topLevelItemGen, count).Select(items => items.ToList()));

        return Prop.ForAll(Arb.From(navItemsGen), (List<NavItem> navItems) =>
        {
            // Arrange: Create a mock INavigationProvider returning the generated items
            var mockProvider = new Mock<INavigationProvider>();
            mockProvider.Setup(p => p.GetMainMenuItems())
                .Returns(navItems.AsReadOnly());

            // Create the PagePermissions component with the mocked provider
            var page = CreatePagePermissionsWithProvider(mockProvider.Object);

            // Act: Call ExtractPageRows to get the matrix page list
            var pageRows = InvokeExtractPageRows(page);

            // Assert: No System_Page path should be present in the extracted rows
            var systemPagesSet = new HashSet<string>(SystemPagePaths, StringComparer.OrdinalIgnoreCase);
            var violatingPages = pageRows
                .Where(row => systemPagesSet.Contains(row.PagePath))
                .Select(row => row.PagePath)
                .ToList();

            return (violatingPages.Count == 0)
                .Label($"NavItems={navItems.Count}, ExtractedRows={pageRows.Count}, " +
                       $"Violations=[{string.Join(", ", violatingPages)}]");
        });
    }

    /// <summary>
    /// Property: For any navigation structure that contains at least one system page,
    /// the count of extracted page rows SHALL be strictly less than the total count of
    /// Link NavItems in the structure (proving system pages are filtered out).
    /// <para><b>Validates: Requirements 8.10</b></para>
    /// </summary>
    [Property(MaxTest = 2)]
    public Property ExtractPageRows_ResultCount_LessThanTotalLinks_WhenSystemPagesPresent()
    {
        // Always include at least one system page Link in the generated structure
        var systemLinkGen = Gen.Elements(SystemPageHrefs)
            .Select(href => new NavItem { Type = NavItemType.Link, Text = $"System: {href}", Href = href });

        var regularLinkGen = Gen.Elements(RegularPageHrefs)
            .Select(href => new NavItem { Type = NavItemType.Link, Text = $"Page: {href}", Href = href });

        // Generate 1-3 system page links (guaranteed to exist)
        var systemLinksGen = Gen.Choose(1, 3).SelectMany<int, NavItem[]>(count =>
            Gen.ArrayOf(systemLinkGen, count));

        // Generate 1-5 regular links
        var regularLinksGen = Gen.Choose(1, 5).SelectMany<int, NavItem[]>(count =>
            Gen.ArrayOf(regularLinkGen, count));

        // Combine: guaranteed system pages + regular pages
        var navItemsGen = systemLinksGen.SelectMany<NavItem[], List<NavItem>>(systemLinks =>
            regularLinksGen.Select(regularLinks =>
                systemLinks.Concat(regularLinks).ToList()));

        return Prop.ForAll(Arb.From(navItemsGen), (List<NavItem> navItems) =>
        {
            // Arrange
            var mockProvider = new Mock<INavigationProvider>();
            mockProvider.Setup(p => p.GetMainMenuItems())
                .Returns(navItems.AsReadOnly());

            var page = CreatePagePermissionsWithProvider(mockProvider.Object);

            // Act
            var pageRows = InvokeExtractPageRows(page);

            // Count total Link items in the input
            var totalLinks = navItems.Count(item => item.Type == NavItemType.Link && !string.IsNullOrEmpty(item.Href));

            // Count system page links in the input
            var systemPagesSet = new HashSet<string>(SystemPageHrefs, StringComparer.OrdinalIgnoreCase);
            var systemLinksCount = navItems.Count(item =>
                item.Type == NavItemType.Link &&
                !string.IsNullOrEmpty(item.Href) &&
                systemPagesSet.Contains(item.Href));

            // Assert: extracted rows should be total links minus system page links
            return (pageRows.Count == totalLinks - systemLinksCount)
                .Label($"TotalLinks={totalLinks}, SystemLinks={systemLinksCount}, " +
                       $"ExtractedRows={pageRows.Count}, Expected={totalLinks - systemLinksCount}");
        });
    }
}
