// ============================================================================
// REFERENCE FILE: Original NavMenu filtering pipeline
// ============================================================================
// This file preserves the original client-side filtering logic that was removed
// from NavMenu.razor.cs as part of the API navigation filtering feature.
// It is kept as a reference until Property 1 (Filtering Pipeline Equivalence)
// confirms that the API-side NavigationService produces identical results.
//
// DO NOT compile this file — it exists solely for reference comparison.
// Delete this file once equivalence is verified via property-based tests.
// ============================================================================

#if false // Excluded from compilation — reference only

using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Core.Common.Defaults;
using AspireWebAppTemplate.Web.Abstractions;

namespace AspireWebAppTemplate.Web.Components.Layout.Sidebar;

public partial class NavMenu
{
    // --- Dependencies that were previously injected ---
    // [Inject] private IPagePermissionContext PagePermissionContext { get; set; } = default!;
    // [CascadingParameter] private Task<AuthenticationState> AuthStateTask { get; set; } = default!;
    // [Parameter] public IReadOnlyList<NavItem> Items { get; set; } = [];
    // private bool _isAuthenticated;

    #region Navigation Filtering (REMOVED — now handled by API NavigationService)

    /// <summary>
    /// Computes the final list of navigation items to render by filtering for accessibility
    /// and removing orphaned decorative items (headers/dividers without adjacent content).
    /// Called once when inputs change, not on every render.
    /// </summary>
    /// <param name="items">The source navigation items from the Items parameter.</param>
    /// <returns>A flat list of items guaranteed to be visible and properly decorated.</returns>
    private List<NavItem> ComputeVisibleNavItems(IReadOnlyList<NavItem> items)
    {
        var accessible = FilterByAccessibility(items);
        return RemoveOrphanedDecorations(accessible);
    }

    /// <summary>
    /// Recursively filters navigation items by authentication state and page permissions.
    /// Links are checked individually; Groups are included only if they have visible children.
    /// Headers and Dividers pass through (handled in the decoration pass).
    /// </summary>
    private List<NavItem> FilterByAccessibility(IReadOnlyList<NavItem> items)
    {
        var result = new List<NavItem>();

        foreach (var item in items)
        {
            switch (item.Type)
            {
                case NavItemType.Header:
                case NavItemType.Divider:
                    if (IsAuthVisible(item))
                        result.Add(item);
                    break;

                case NavItemType.Link:
                    if (IsAuthVisible(item) && IsPageAccessible(item))
                        result.Add(item);
                    break;

                case NavItemType.Group:
                    if (!IsAuthVisible(item))
                        break;

                    var visibleChildren = FilterByAccessibility(item.Children ?? []);
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
                            Children = visibleChildren,
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
    /// </summary>
    private static List<NavItem> RemoveOrphanedDecorations(List<NavItem> items)
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

    /// <summary>
    /// Determines whether a navigation link is accessible based on page permissions and system pages.
    /// </summary>
    private bool IsPageAccessible(NavItem item)
    {
        if (item.Href is null)
            return true;

        if (item.Href == "")
        {
            if (SystemPageDefaults.Paths.Contains("/"))
                return true;
            if (!PagePermissionContext.IsLoaded)
                return false;
            return PagePermissionContext.CanAccess("/");
        }

        var fullPath = "/" + item.Href;

        if (SystemPageDefaults.Paths.Contains(fullPath))
            return true;

        if (!PagePermissionContext.IsLoaded)
            return false;

        return PagePermissionContext.CanAccess(fullPath);
    }

    /// <summary>
    /// Determines whether an item should be visible based on authentication state.
    /// </summary>
    private bool IsAuthVisible(NavItem item)
    {
        if (item.AuthorizedOnly && !_isAuthenticated)
            return false;
        if (item.NotAuthorizedOnly && _isAuthenticated)
            return false;
        return true;
    }

    #endregion
}

#endif
