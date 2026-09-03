using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Application.Features.Template.Navigation;
using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Domain.Constants;

namespace AspireWebAppTemplate.Application.Extensions;

/// <summary>
/// Extension methods for <see cref="INavigationProvider"/> that provide common
/// navigation tree traversal operations used across the application.
/// </summary>
public static class NavigationProviderExtensions
{
    /// <summary>
    /// Extracts all Link-type NavItems from the navigation hierarchy as a flat list
    /// of (PagePath, PageDisplayName) tuples with normalized paths (prefixed with "/").
    /// Excludes System_Pages that bypass all permission checks.
    /// </summary>
    /// <param name="provider">The navigation provider to extract links from.</param>
    /// <returns>A list of tuples containing the normalized page path and display name.</returns>
    public static IReadOnlyList<(string PagePath, string DisplayName)> GetAllLinkPages(this INavigationProvider provider)
    {
        var pages = new List<(string PagePath, string DisplayName)>();
        ExtractLinksRecursive(provider.GetMainMenuItems(), pages);
        return pages;
    }

    /// <summary>
    /// Gets all valid page paths from the navigation provider as a case-insensitive set.
    /// Excludes System_Pages. Useful for validating page paths in permission operations.
    /// </summary>
    /// <param name="provider">The navigation provider to extract paths from.</param>
    /// <returns>A case-insensitive set of all valid page paths (normalized with "/" prefix).</returns>
    public static IReadOnlySet<string> GetAllValidPagePaths(this INavigationProvider provider)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (pagePath, _) in provider.GetAllLinkPages())
        {
            paths.Add(pagePath);
        }

        return paths;
    }

    /// <summary>
    /// Gets a case-insensitive dictionary mapping normalized page paths to their display names.
    /// Excludes System_Pages. Useful for looking up display names when creating permission records.
    /// </summary>
    /// <param name="provider">The navigation provider to extract names from.</param>
    /// <returns>A case-insensitive dictionary of page path → display name.</returns>
    public static IReadOnlyDictionary<string, string> GetPageDisplayNameMap(this INavigationProvider provider)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (pagePath, displayName) in provider.GetAllLinkPages())
        {
            map.TryAdd(pagePath, displayName);
        }

        return map;
    }

    /// <summary>
    /// Recursively walks the NavItem tree and collects Link items with their
    /// normalized paths and display names. Excludes System_Pages.
    /// </summary>
    private static void ExtractLinksRecursive(IReadOnlyList<NavItem> items, List<(string PagePath, string DisplayName)> pages)
    {
        foreach (var item in items)
        {
            if (item.Type == NavItemType.Link && item.Href is not null)
            {
                // Normalize: empty string "" represents the Home page (root path "/").
                var pagePath = string.IsNullOrEmpty(item.Href)
                    ? "/"
                    : item.Href.StartsWith('/')
                        ? item.Href
                        : "/" + item.Href;

                // Exclude System_Pages — they bypass all permission checks
                if (!SystemPageDefaults.Paths.Contains(pagePath))
                {
                    pages.Add((pagePath, item.Text));
                }
            }

            // Recurse into group children to find nested Link items
            if (item.Children is not null)
            {
                ExtractLinksRecursive(item.Children, pages);
            }
        }
    }
}
