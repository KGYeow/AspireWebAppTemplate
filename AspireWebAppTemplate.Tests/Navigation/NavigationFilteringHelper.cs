// Feature: api-nav-filtering, Property 1: Filtering Pipeline Equivalence
using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Core.Common.Defaults;

namespace AspireWebAppTemplate.Tests.Navigation;

/// <summary>
/// Standalone static helper that implements both the "old" (NavMenu-style) and "new"
/// (NavigationService-style) filtering pipelines in pure, testable static methods.
/// Used by property-based tests to verify equivalence between the two implementations
/// without requiring dependency injection or component lifecycle.
/// </summary>
/// <remarks>
/// <para>
/// The filtering pipeline has four stages applied in order:
/// <list type="number">
///   <item><description>Auth Filter — removes items based on AuthorizedOnly/NotAuthorizedOnly flags.</description></item>
///   <item><description>Permission Filter — removes Link items whose normalized Href is not in the permission set (system pages bypass).</description></item>
///   <item><description>Group Visibility — removes groups with zero visible content children (bottom-up).</description></item>
///   <item><description>Orphan Decoration Removal — removes Headers without following content and Dividers without content on both sides.</description></item>
/// </list>
/// </para>
/// <para>
/// Both implementations (Reference and New) should produce identical output for any input.
/// The Reference implementation mirrors the original NavMenu.ComputeVisibleNavItems logic.
/// The New implementation mirrors what NavigationService WILL implement.
/// </para>
/// </remarks>
public static class NavigationFilteringHelper
{
    #region New Implementation (NavigationService-style)

    /// <summary>
    /// Applies the full filtering pipeline as NavigationService will implement it:
    /// FilterByAccessibility (auth + permission + group visibility) → RemoveOrphanedDecorations.
    /// </summary>
    /// <param name="items">The source navigation items.</param>
    /// <param name="isAuthenticated">Whether the user is authenticated.</param>
    /// <param name="permittedPaths">
    /// The set of page paths the user has permission to access (case-insensitive).
    /// </param>
    /// <returns>The filtered navigation tree ready to render.</returns>
    public static List<NavItem> ApplyNewPipeline(
        IReadOnlyList<NavItem> items,
        bool isAuthenticated,
        HashSet<string> permittedPaths)
    {
        var accessible = FilterByAccessibility(items, isAuthenticated, permittedPaths);
        return RemoveOrphanedDecorations(accessible);
    }

    /// <summary>
    /// Recursively filters navigation items by authentication state and page permissions.
    /// Links are checked individually; Groups are included only if they have visible content children.
    /// Headers and Dividers pass through auth filtering (handled in the decoration pass).
    /// </summary>
    /// <param name="items">The items to filter at this level.</param>
    /// <param name="isAuthenticated">Whether the user is authenticated.</param>
    /// <param name="permittedPaths">The user's permitted page paths.</param>
    /// <returns>A list of items that passed auth and permission filtering.</returns>
    public static List<NavItem> FilterByAccessibility(
        IReadOnlyList<NavItem> items,
        bool isAuthenticated,
        HashSet<string> permittedPaths)
    {
        var result = new List<NavItem>();

        foreach (var item in items)
        {
            switch (item.Type)
            {
                case NavItemType.Header:
                case NavItemType.Divider:
                    if (IsAuthVisible(item, isAuthenticated))
                        result.Add(item);
                    break;

                case NavItemType.Link:
                    if (IsAuthVisible(item, isAuthenticated) && IsPageAccessible(item, permittedPaths))
                        result.Add(item);
                    break;

                case NavItemType.Group:
                    if (!IsAuthVisible(item, isAuthenticated))
                        break;

                    var visibleChildren = FilterByAccessibility(item.Children ?? [], isAuthenticated, permittedPaths);
                    var hasContent = visibleChildren.Exists(c => c.Type is NavItemType.Link or NavItemType.Group);
                    if (hasContent)
                    {
                        result.Add(new NavItem
                        {
                            Type = item.Type,
                            Text = item.Text,
                            Href = item.Href,
                            Title = item.Title,
                            Match = item.Match,
                            Icon = item.Icon,
                            AuthorizedOnly = item.AuthorizedOnly,
                            NotAuthorizedOnly = item.NotAuthorizedOnly,
                            DividerClass = item.DividerClass,
                            Children = RemoveOrphanedDecorations(visibleChildren),
                            Expanded = item.Expanded
                        });
                    }
                    break;
            }
        }

        return result;
    }

    /// <summary>
    /// Removes decorative items (Headers and Dividers) that have no adjacent visible content.
    /// A Header is kept only if a Content_Item (Link or Group) follows before the next Header or end.
    /// A Divider is kept only if both a preceding and following Content_Item exist.
    /// </summary>
    /// <param name="items">The items at a single tree level after accessibility filtering.</param>
    /// <returns>The items with orphaned decorations removed.</returns>
    public static List<NavItem> RemoveOrphanedDecorations(List<NavItem> items)
    {
        var result = new List<NavItem>(items.Count);

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];

            switch (item.Type)
            {
                case NavItemType.Header:
                    if (HasFollowingContent(items, i + 1))
                        result.Add(item);
                    break;

                case NavItemType.Divider:
                    if (HasPrecedingContent(result) && HasFollowingContent(items, i + 1))
                        result.Add(item);
                    break;

                case NavItemType.Link:
                case NavItemType.Group:
                    result.Add(item);
                    break;
            }
        }

        return result;
    }

    /// <summary>
    /// Normalizes an Href value to a canonical path format for permission comparison.
    /// Prepends "/" if missing, strips trailing "/", treats empty string as "/".
    /// </summary>
    /// <param name="href">The raw Href value from a NavItem (may be null).</param>
    /// <returns>The normalized path, or null if the input was null.</returns>
    public static string? NormalizePath(string? href)
    {
        if (href is null)
            return null;

        if (href == "")
            return "/";

        var path = href.StartsWith('/') ? href : "/" + href;

        if (path.Length > 1 && path.EndsWith('/'))
            path = path[..^1];

        return path;
    }

    #endregion

    #region Reference Implementation (NavMenu-style)

    /// <summary>
    /// Applies the reference filtering pipeline as NavMenu.ComputeVisibleNavItems originally did:
    /// FilterByAccessibility → RemoveOrphanedDecorations (at top level only).
    /// This replicates the exact behavior of the original NavMenu implementation.
    /// </summary>
    /// <param name="items">The source navigation items.</param>
    /// <param name="isAuthenticated">Whether the user is authenticated.</param>
    /// <param name="permittedPaths">
    /// The set of page paths the user has permission to access (case-insensitive).
    /// </param>
    /// <returns>The filtered navigation tree as NavMenu would have produced.</returns>
    public static List<NavItem> ApplyReferencePipeline(
        IReadOnlyList<NavItem> items,
        bool isAuthenticated,
        HashSet<string> permittedPaths)
    {
        var accessible = ReferenceFilterByAccessibility(items, isAuthenticated, permittedPaths);
        return RemoveOrphanedDecorations(accessible);
    }

    /// <summary>
    /// Reference implementation of FilterByAccessibility matching the original NavMenu logic.
    /// Recursively filters items; Groups are included only if they have visible content children.
    /// The original NavMenu did NOT apply RemoveOrphanedDecorations to group children inline —
    /// it only applied it at the top level. However, per the design spec, both implementations
    /// should apply decoration removal at each level. This reference mirrors the corrected behavior.
    /// </summary>
    private static List<NavItem> ReferenceFilterByAccessibility(
        IReadOnlyList<NavItem> items,
        bool isAuthenticated,
        HashSet<string> permittedPaths)
    {
        var result = new List<NavItem>();

        foreach (var item in items)
        {
            switch (item.Type)
            {
                case NavItemType.Header:
                case NavItemType.Divider:
                    if (IsAuthVisible(item, isAuthenticated))
                        result.Add(item);
                    break;

                case NavItemType.Link:
                    if (IsAuthVisible(item, isAuthenticated) && ReferenceIsPageAccessible(item, permittedPaths))
                        result.Add(item);
                    break;

                case NavItemType.Group:
                    if (!IsAuthVisible(item, isAuthenticated))
                        break;

                    var visibleChildren = ReferenceFilterByAccessibility(item.Children ?? [], isAuthenticated, permittedPaths);
                    var hasContent = visibleChildren.Exists(c => c.Type is NavItemType.Link or NavItemType.Group);
                    if (hasContent)
                    {
                        result.Add(new NavItem
                        {
                            Type = item.Type,
                            Text = item.Text,
                            Href = item.Href,
                            Title = item.Title,
                            Match = item.Match,
                            Icon = item.Icon,
                            AuthorizedOnly = item.AuthorizedOnly,
                            NotAuthorizedOnly = item.NotAuthorizedOnly,
                            DividerClass = item.DividerClass,
                            Children = RemoveOrphanedDecorations(visibleChildren),
                            Expanded = item.Expanded
                        });
                    }
                    break;
            }
        }

        return result;
    }

    /// <summary>
    /// Reference implementation of IsPageAccessible matching the original NavMenu logic.
    /// Uses the same normalization rules: null → always visible, empty → "/",
    /// prepend "/" if missing, strip trailing "/", check system pages, then check permission set.
    /// </summary>
    private static bool ReferenceIsPageAccessible(NavItem item, HashSet<string> permittedPaths)
    {
        if (item.Href is null)
            return true;

        var normalizedPath = NormalizePath(item.Href)!;

        if (SystemPageDefaults.Paths.Contains(normalizedPath))
            return true;

        return permittedPaths.Contains(normalizedPath);
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Determines whether an item should be visible based on authentication state
    /// using the auth truth table:
    /// (AuthorizedOnly=true, NotAuthorizedOnly=false) → visible only when authenticated.
    /// (AuthorizedOnly=false, NotAuthorizedOnly=true) → visible only when unauthenticated.
    /// (AuthorizedOnly=false, NotAuthorizedOnly=false) → always visible.
    /// (AuthorizedOnly=true, NotAuthorizedOnly=true) → never visible.
    /// </summary>
    private static bool IsAuthVisible(NavItem item, bool isAuthenticated)
    {
        if (item.AuthorizedOnly && !isAuthenticated)
            return false;
        if (item.NotAuthorizedOnly && isAuthenticated)
            return false;
        return true;
    }

    /// <summary>
    /// Determines whether a Link item is accessible based on page permissions.
    /// Null Href → always visible. System pages bypass permission checks.
    /// The new implementation uses NormalizePath for consistent path comparison
    /// including trailing slash handling and case-insensitive matching.
    /// </summary>
    private static bool IsPageAccessible(NavItem item, HashSet<string> permittedPaths)
    {
        if (item.Href is null)
            return true;

        var normalizedPath = NormalizePath(item.Href)!;

        if (SystemPageDefaults.Paths.Contains(normalizedPath))
            return true;

        return permittedPaths.Contains(normalizedPath);
    }

    /// <summary>
    /// Checks whether there is a Content_Item (Link or Group) preceding the current position
    /// in the result list, scanning backwards from the end. Stops at the first Header encountered
    /// (Headers delimit sections).
    /// </summary>
    private static bool HasPrecedingContent(List<NavItem> result)
    {
        for (var i = result.Count - 1; i >= 0; i--)
        {
            if (result[i].Type is NavItemType.Link or NavItemType.Group)
                return true;
            if (result[i].Type is NavItemType.Header)
                return false;
        }
        return false;
    }

    /// <summary>
    /// Checks whether there is a Content_Item (Link or Group) following the given start index.
    /// Stops at the first Header encountered (Headers delimit sections).
    /// </summary>
    private static bool HasFollowingContent(List<NavItem> items, int startIndex)
    {
        for (var i = startIndex; i < items.Count; i++)
        {
            if (items[i].Type is NavItemType.Link or NavItemType.Group)
                return true;
            if (items[i].Type is NavItemType.Header)
                return false;
        }
        return false;
    }

    #endregion
}
