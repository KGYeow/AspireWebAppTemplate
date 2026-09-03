using AspireWebAppTemplate.Application.Common;

namespace AspireWebAppTemplate.Application.Features.Template.Navigation;

/// <summary>
/// Defines the contract for the navigation service that provides filtered navigation trees
/// based on the current user's authentication state and page permissions.
/// </summary>
/// <remarks>
/// Implementations execute the full filtering pipeline in order:
/// <list type="number">
///   <item><description>
///     <b>Auth filter</b> — removes items based on <see cref="NavItem.AuthorizedOnly"/> and
///     <see cref="NavItem.NotAuthorizedOnly"/> flags relative to the user's authentication state.
///   </description></item>
///   <item><description>
///     <b>Permission filter</b> — removes Link items whose normalized Href is not in the user's
///     page permission set (system pages bypass this check).
///   </description></item>
///   <item><description>
///     <b>Group visibility resolution</b> — removes groups with zero visible content children,
///     evaluated bottom-up so that empty nested groups do not count as visible children.
///   </description></item>
///   <item><description>
///     <b>Orphan decoration removal</b> — removes Headers without following content and Dividers
///     without content on both sides, applied independently at each tree level.
///   </description></item>
/// </list>
/// Implementations should be registered as scoped services to align with the per-request
/// <c>DbContext</c> lifetime.
/// </remarks>
public interface INavigationService
{
    #region Query Operations

    /// <summary>
    /// Returns the navigation tree filtered for the current authenticated user.
    /// The returned list contains only items the user is permitted to see,
    /// with empty groups removed and orphan decorations cleaned up.
    /// </summary>
    /// <returns>
    /// A task that resolves to a list of <see cref="NavItem"/> objects representing the
    /// filtered navigation tree. Returns an empty list if the user has no permitted pages.
    /// </returns>
    Task<List<NavItem>> GetFilteredNavigationAsync();

    #endregion
}
