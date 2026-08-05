// Feature: api-nav-filtering, Generators for Property Tests
using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Domain.Constants;
using FsCheck;
using FsCheck.Fluent;
using Gen = FsCheck.Fluent.Gen;

namespace AspireWebAppTemplate.Tests.Navigation.Generators;

/// <summary>
/// Shared FsCheck generators for navigation property-based tests.
/// Provides generators for NavItem trees, permission sets, authentication states,
/// and href values with normalization edge cases.
/// </summary>
/// <remarks>
/// <para>
/// These generators are used by all six property test classes in the navigation
/// filtering test suite. They produce structurally valid NavItem trees that exercise
/// the full input space of the filtering pipeline.
/// </para>
/// <para>
/// <b>Validates: Requirements 7.1</b>
/// </para>
/// </remarks>
public static class NavItemGenerators
{
    #region Constants

    /// <summary>
    /// Known page paths used in the application. Used by <see cref="GenPermissionSet"/>
    /// to generate realistic permission subsets.
    /// </summary>
    private static readonly string[] KnownPagePaths =
    [
        "/",
        "/counter",
        "/weather",
        "/auth",
        "/account/notifications",
        "/account/settings",
        "/account/profile",
        "/admin/user-management",
        "/admin/role-management",
        "/admin/audit-log",
        "/admin/page-permissions",
        "/dashboard",
        "/reports"
    ];

    /// <summary>
    /// Text values used for generated NavItem labels.
    /// </summary>
    private static readonly string[] ItemTexts =
    [
        "Home", "Counter", "Weather", "Auth Status", "Notifications",
        "User Management", "Role Management", "Audit Log", "Page Permissions",
        "Dashboard", "Reports", "Settings", "Profile", "Activity", "Administration"
    ];

    /// <summary>
    /// Icon values used for generated NavItem icons.
    /// </summary>
    private static readonly string[] Icons =
    [
        "material-symbols-rounded/home",
        "material-symbols-rounded/plus_one",
        "material-symbols-rounded/partly_cloudy_day",
        "material-symbols-rounded/lock",
        "material-symbols-rounded/group",
        "material-symbols-rounded/history",
        "material-symbols-rounded/admin_panel_settings",
        "material-symbols-rounded/notifications",
        "material-symbols-rounded/apps"
    ];

    /// <summary>
    /// Divider CSS class values for generated Divider items.
    /// </summary>
    private static readonly string[] DividerClasses =
    [
        "my-2",
        "my-1",
        "my-3"
    ];

    #endregion

    #region Public Generators

    /// <summary>
    /// Generates a random NavItem of any type with recursive children for Group items.
    /// Depth is bounded by <paramref name="maxDepth"/> to prevent infinite recursion.
    /// </summary>
    /// <param name="maxDepth">
    /// Maximum nesting depth for Group children. When 0, only leaf items
    /// (Header, Link, Divider) are generated — no Groups.
    /// </param>
    /// <returns>A generator producing random NavItem instances.</returns>
    public static Gen<NavItem> GenNavItem(int maxDepth)
    {
        var headerGen = GenHeader();
        var linkGen = GenLink();
        var dividerGen = GenDivider();

        if (maxDepth <= 0)
        {
            // At max depth, only generate leaf items (no Groups)
            return Gen.OneOf(headerGen, linkGen, linkGen, dividerGen);
        }

        var groupGen = GenGroup(maxDepth);

        // Weight Links more heavily since they are the most common item type
        return Gen.Frequency(
            (2, headerGen),
            (5, linkGen),
            (1, dividerGen),
            (3, groupGen));
    }

    /// <summary>
    /// Generates a list of NavItems forming a valid navigation tree.
    /// The tree can be up to <paramref name="maxDepth"/> levels deep with up to
    /// <paramref name="maxWidth"/> items per level.
    /// </summary>
    /// <param name="maxDepth">Maximum nesting depth (up to 5 levels deep).</param>
    /// <param name="maxWidth">Maximum number of items per level (up to 50 items).</param>
    /// <returns>A generator producing a list of NavItem instances forming a tree.</returns>
    public static Gen<List<NavItem>> GenNavTree(int maxDepth, int maxWidth)
    {
        var clampedDepth = Math.Clamp(maxDepth, 1, 5);
        var clampedWidth = Math.Clamp(maxWidth, 1, 50);

        return Gen.Choose(1, clampedWidth).SelectMany<int, List<NavItem>>(count =>
            Gen.ArrayOf(GenNavItem(clampedDepth), count)
                .Select(items => items.ToList()));
    }

    /// <summary>
    /// Generates a random subset of known page paths to simulate a user's
    /// page permission set. Includes the possibility of an empty set
    /// (user has no permissions).
    /// </summary>
    /// <returns>
    /// A generator producing a <see cref="HashSet{String}"/> with OrdinalIgnoreCase
    /// comparison, containing zero or more page paths.
    /// </returns>
    public static Gen<HashSet<string>> GenPermissionSet()
    {
        return Gen.SubListOf(KnownPagePaths)
            .Select(paths => new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Generates a random authentication state: authenticated (true) or
    /// unauthenticated (false).
    /// </summary>
    /// <returns>A generator producing a boolean authentication state.</returns>
    public static Gen<bool> GenAuthState()
    {
        return Gen.Elements(true, false);
    }

    /// <summary>
    /// Generates href values with normalization edge cases including null, empty string,
    /// paths with/without leading slash, paths with trailing slash, and mixed case variants.
    /// These exercise the path normalization logic defined in Requirement 8.
    /// </summary>
    /// <returns>A generator producing nullable href strings covering normalization edge cases.</returns>
    public static Gen<string?> GenHref()
    {
        // Null href (always visible, skip permission check)
        var nullGen = Gen.Constant<string?>(null);

        // Empty string href (normalizes to "/")
        var emptyGen = Gen.Constant<string?>("");

        // Standard hrefs without leading slash (e.g., "counter", "admin/audit-log")
        var noLeadingSlashGen = Gen.Elements<string?>(
            "counter", "weather", "auth", "admin/audit-log",
            "admin/user-management", "admin/role-management",
            "admin/page-permissions", "account/notifications",
            "account/settings", "account/profile");

        // Hrefs with leading slash (e.g., "/counter", "/admin/audit-log")
        var withLeadingSlashGen = Gen.Elements<string?>(
            "/counter", "/weather", "/auth", "/admin/audit-log",
            "/admin/user-management", "/admin/role-management",
            "/admin/page-permissions", "/account/notifications");

        // Hrefs with trailing slash (e.g., "counter/", "admin/audit-log/")
        var withTrailingSlashGen = Gen.Elements<string?>(
            "counter/", "weather/", "admin/audit-log/",
            "admin/user-management/", "admin/role-management/",
            "/admin/page-permissions/", "/account/notifications/");

        // Mixed case variants (e.g., "Counter", "ADMIN/Audit-Log", "/Admin/User-Management")
        var mixedCaseGen = Gen.Elements<string?>(
            "Counter", "WEATHER", "Admin/Audit-Log",
            "ADMIN/user-management", "/Admin/Role-Management",
            "Account/Notifications", "/COUNTER", "ADMIN/PAGE-PERMISSIONS");

        return Gen.Frequency(
            (1, nullGen),
            (1, emptyGen),
            (4, noLeadingSlashGen),
            (2, withLeadingSlashGen),
            (2, withTrailingSlashGen),
            (2, mixedCaseGen));
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Generates a Header-type NavItem with random text.
    /// </summary>
    private static Gen<NavItem> GenHeader()
    {
        return GenAuthFlags().SelectMany<(bool authOnly, bool notAuthOnly), NavItem>(flags =>
            Gen.Elements(ItemTexts).Select(text => new NavItem
            {
                Type = NavItemType.Header,
                Text = text,
                AuthorizedOnly = flags.authOnly,
                NotAuthorizedOnly = flags.notAuthOnly
            }));
    }

    /// <summary>
    /// Generates a Link-type NavItem with random href, text, icon, match, and auth flags.
    /// </summary>
    private static Gen<NavItem> GenLink()
    {
        return GenHref().SelectMany<string?, NavItem>(href =>
            Gen.Elements(ItemTexts).SelectMany<string, NavItem>(text =>
                GenOptionalIcon().SelectMany<string?, NavItem>(icon =>
                    Gen.Elements(NavMatch.Exact, NavMatch.Prefix).SelectMany<NavMatch, NavItem>(match =>
                        GenAuthFlags().Select(flags => new NavItem
                        {
                            Type = NavItemType.Link,
                            Text = text,
                            Href = href,
                            Title = text,
                            Match = match,
                            Icon = icon,
                            AuthorizedOnly = flags.authOnly,
                            NotAuthorizedOnly = flags.notAuthOnly
                        })))));
    }

    /// <summary>
    /// Generates a Divider-type NavItem with optional DividerClass.
    /// </summary>
    private static Gen<NavItem> GenDivider()
    {
        return GenAuthFlags().SelectMany<(bool authOnly, bool notAuthOnly), NavItem>(flags =>
            Gen.Frequency(
                (3, Gen.Elements(DividerClasses).Select<string, string?>(c => c)),
                (1, Gen.Constant<string?>(null)))
            .Select(divClass => new NavItem
            {
                Type = NavItemType.Divider,
                DividerClass = divClass,
                AuthorizedOnly = flags.authOnly,
                NotAuthorizedOnly = flags.notAuthOnly
            }));
    }

    /// <summary>
    /// Generates a Group-type NavItem with recursive children bounded by depth.
    /// The group contains 1–6 child items at the next depth level.
    /// </summary>
    private static Gen<NavItem> GenGroup(int maxDepth)
    {
        return Gen.Elements(ItemTexts).SelectMany<string, NavItem>(text =>
            GenOptionalIcon().SelectMany<string?, NavItem>(icon =>
                GenAuthFlags().SelectMany<(bool authOnly, bool notAuthOnly), NavItem>(flags =>
                    Gen.Elements<bool?>(true, false, null).SelectMany<bool?, NavItem>(expanded =>
                        Gen.Choose(1, 6).SelectMany<int, NavItem>(childCount =>
                            Gen.ArrayOf(GenNavItem(maxDepth - 1), childCount)
                                .Select(children => new NavItem
                                {
                                    Type = NavItemType.Group,
                                    Text = text,
                                    Icon = icon,
                                    AuthorizedOnly = flags.authOnly,
                                    NotAuthorizedOnly = flags.notAuthOnly,
                                    Children = children.ToList(),
                                    Expanded = expanded
                                }))))));
    }

    /// <summary>
    /// Generates a tuple of (AuthorizedOnly, NotAuthorizedOnly) boolean flags.
    /// All four combinations are possible: (false,false), (true,false), (false,true), (true,true).
    /// </summary>
    private static Gen<(bool authOnly, bool notAuthOnly)> GenAuthFlags()
    {
        return Gen.Elements(true, false).SelectMany<bool, (bool authOnly, bool notAuthOnly)>(authOnly =>
            Gen.Elements(true, false).Select(notAuthOnly => (authOnly, notAuthOnly)));
    }

    /// <summary>
    /// Generates an optional icon string — either a random icon or null.
    /// </summary>
    private static Gen<string?> GenOptionalIcon()
    {
        return Gen.Frequency(
            (3, Gen.Elements(Icons).Select<string, string?>(i => i)),
            (1, Gen.Constant<string?>(null)));
    }

    #endregion
}
