using AspireWebAppTemplate.ApiService.Abstractions;
using AspireWebAppTemplate.Core.Application.Abstractions;
using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Core.Common.Defaults;

namespace AspireWebAppTemplate.ApiService.Services;

/// <summary>
/// Provides filtered navigation trees by combining the full navigation structure
/// with the current user's authentication state and page permissions.
/// Implements the full filtering pipeline: auth filter → permission filter →
/// group visibility resolution → orphan decoration removal.
/// </summary>
/// <remarks>
/// <para>
/// This service is the single source of truth for navigation visibility.
/// </para>
/// <para>
/// Registered as a scoped service to align with the per-request <c>DbContext</c> lifetime.
/// </para>
/// </remarks>
public class NavigationService : INavigationService
{
    #region Constructor

    /// <summary>
    /// Provides the full navigation tree (all items before filtering).
    /// </summary>
    private readonly INavigationProvider _navigationProvider;

    /// <summary>
    /// Provides page permission lookups for the current user.
    /// </summary>
    private readonly IPagePermissionService _pagePermissionService;

    /// <summary>
    /// Provides the current authenticated user's identity.
    /// </summary>
    private readonly ICurrentUserAccessor _currentUserAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationService"/> class.
    /// </summary>
    /// <param name="navigationProvider">The provider of the full navigation tree.</param>
    /// <param name="pagePermissionService">The service for retrieving user page permissions.</param>
    /// <param name="currentUserAccessor">The accessor for the current user's identity.</param>
    public NavigationService(
        INavigationProvider navigationProvider,
        IPagePermissionService pagePermissionService,
        ICurrentUserAccessor currentUserAccessor)
    {
        _navigationProvider = navigationProvider;
        _pagePermissionService = pagePermissionService;
        _currentUserAccessor = currentUserAccessor;
    }

    #endregion

    #region Filtering Pipeline

    /// <inheritdoc/>
    public async Task<List<NavItem>> GetFilteredNavigationAsync()
    {
        var allItems = _navigationProvider.GetMainMenuItems();
        var userId = _currentUserAccessor.UserId;
        var isAuthenticated = userId is not null;
        var permittedPaths = isAuthenticated
            ? new HashSet<string>(await _pagePermissionService.GetMyPagesAsync(userId!), StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var accessible = FilterByAccessibility(allItems, isAuthenticated, permittedPaths);
        return RemoveOrphanedDecorations(accessible);
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Recursively filters navigation items by authentication state and page permissions.
    /// Links are checked individually; Groups are included only if they have visible content children.
    /// Headers and Dividers pass through auth check only (handled in the decoration pass).
    /// </summary>
    /// <param name="items">The items to filter at the current tree level.</param>
    /// <param name="isAuthenticated">Whether the current user is authenticated.</param>
    /// <param name="permittedPaths">The set of page paths the user has permission to access.</param>
    /// <returns>A filtered list of NavItems at this level.</returns>
    private static List<NavItem> FilterByAccessibility(IReadOnlyList<NavItem> items, bool isAuthenticated, HashSet<string> permittedPaths)
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
    /// A Header is included only if followed by a Content_Item before the next Header or end.
    /// A Divider is included only if it has both preceding and following Content_Items.
    /// Applied at each tree level independently.
    /// </summary>
    /// <param name="items">The accessibility-filtered items at one tree level.</param>
    /// <returns>Items with orphan decorations removed.</returns>
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
                    if (HasPrecedingContent(result) && HasFollowingContentForDivider(items, i + 1))
                        result.Add(item);
                    break;

                case NavItemType.Link:
                    result.Add(item);
                    break;

                case NavItemType.Group:
                    // Recursively clean decorations within the group's children
                    var cleanChildren = RemoveOrphanedDecorations(item.Children?.ToList() ?? []);
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
                        Children = cleanChildren,
                        Expanded = item.Expanded
                    });
                    break;
            }
        }

        return result;
    }

    /// <summary>
    /// Checks whether there is a Content_Item (Link or Group) preceding the current position
    /// in the result list, scanning backwards until a Header is found (section boundary).
    /// </summary>
    /// <param name="result">The result list built so far.</param>
    /// <returns>True if a preceding Content_Item exists in the current section.</returns>
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
    /// Checks whether there is a Content_Item (Link or Group) following the given start index,
    /// scanning forward until a Header is found (section boundary) or end of list.
    /// Used for Header orphan detection: a Header is orphaned if no content follows in its section.
    /// </summary>
    /// <param name="items">The full items list being processed.</param>
    /// <param name="startIndex">The index to start scanning from.</param>
    /// <returns>True if a following Content_Item exists before the next section boundary.</returns>
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
    /// Checks whether there is a Content_Item (Link or Group) anywhere after the given start index.
    /// Unlike <see cref="HasFollowingContent"/>, this does NOT stop at Headers because Dividers
    /// are inter-section separators — they are valid as long as content exists anywhere after them
    /// in the sibling list, regardless of intervening Headers.
    /// </summary>
    /// <param name="items">The full items list being processed.</param>
    /// <param name="startIndex">The index to start scanning from.</param>
    /// <returns>True if any Content_Item exists in the remaining list.</returns>
    private static bool HasFollowingContentForDivider(List<NavItem> items, int startIndex)
    {
        for (var i = startIndex; i < items.Count; i++)
        {
            if (items[i].Type is NavItemType.Link or NavItemType.Group)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Determines whether a NavItem should be visible based on authentication state.
    /// Implements the auth filtering truth table:
    /// (true,false) → authenticated only; (false,true) → unauthenticated only;
    /// (false,false) → always visible; (true,true) → never visible.
    /// </summary>
    /// <param name="item">The navigation item to evaluate.</param>
    /// <param name="isAuthenticated">Whether the current user is authenticated.</param>
    /// <returns>True if the item passes the auth visibility check.</returns>
    private static bool IsAuthVisible(NavItem item, bool isAuthenticated)
    {
        if (item.AuthorizedOnly && !isAuthenticated)
            return false;
        if (item.NotAuthorizedOnly && isAuthenticated)
            return false;
        return true;
    }

    /// <summary>
    /// Determines whether a Link-type NavItem is accessible based on page permissions.
    /// Null Href is always accessible. System pages bypass permission checks.
    /// Otherwise, the normalized path must exist in the user's permitted paths set.
    /// </summary>
    /// <param name="item">The Link NavItem to evaluate.</param>
    /// <param name="permittedPaths">The set of permitted page paths for the user.</param>
    /// <returns>True if the item is accessible.</returns>
    private static bool IsPageAccessible(NavItem item, HashSet<string> permittedPaths)
    {
        if (item.Href is null)
            return true;

        var normalizedPath = NormalizePath(item.Href);

        if (SystemPageDefaults.Paths.Contains(normalizedPath))
            return true;

        return permittedPaths.Contains(normalizedPath);
    }

    /// <summary>
    /// Normalizes an Href value to a consistent path format for permission comparison.
    /// Prepends "/" if missing, strips trailing "/", and treats empty string as "/".
    /// </summary>
    /// <param name="href">The raw Href value from a NavItem.</param>
    /// <returns>The normalized path string.</returns>
    private static string NormalizePath(string href)
    {
        if (string.IsNullOrEmpty(href))
            return "/";

        var path = href.StartsWith('/') ? href : "/" + href;

        if (path.Length > 1 && path.EndsWith('/'))
            path = path[..^1];

        return path;
    }

    #endregion
}
