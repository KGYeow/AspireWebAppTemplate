namespace AspireWebAppTemplate.Web.Abstractions;

/// <summary>
/// Provides per-circuit page permission state for Blazor Server.
/// Loads the authenticated user's accessible page paths once per circuit and exposes
/// synchronous in-memory lookups for navigation authorization checks.
/// </summary>
/// <remarks>
/// <para>
/// Registered as <b>scoped</b> — one instance per SignalR circuit (user session).
/// Permissions are loaded via <see cref="InitializeAsync"/> at circuit startup and cached
/// in a <see cref="HashSet{T}"/> for O(1) case-insensitive path lookups.
/// </para>
/// <para>
/// The <see cref="PagePermissionHandler"/> and NavMenu component consume this service
/// to enforce role-based page access and filter navigation items respectively.
/// </para>
/// </remarks>
public interface IPagePermissionContext
{
    /// <summary>
    /// Gets a value indicating whether the permission cache has been populated.
    /// Returns <c>true</c> after <see cref="InitializeAsync"/> completes (successfully or with error),
    /// <c>false</c> before initialization.
    /// </summary>
    /// <remarks>
    /// When <c>false</c>, <see cref="CanAccess"/> returns <c>false</c> for all non-System_Pages
    /// to prevent unauthorized access before permissions are loaded.
    /// </remarks>
    bool IsLoaded { get; }

    /// <summary>
    /// Determines whether the current user has permission to access the specified page path.
    /// </summary>
    /// <param name="pagePath">
    /// The route path of the page to check (e.g., "/admin/audit-log").
    /// Comparison is case-insensitive using ordinal rules.
    /// </param>
    /// <returns>
    /// <c>true</c> if the page path exists in the cached accessible pages list or is a System_Page;
    /// <c>false</c> if the path is not permitted or the cache has not yet been loaded.
    /// </returns>
    bool CanAccess(string pagePath);

    /// <summary>
    /// Returns the full list of page paths accessible to the current user.
    /// </summary>
    /// <returns>
    /// A read-only list of page path strings that the user is permitted to access.
    /// Returns an empty list if permissions have not been loaded or the API call failed.
    /// </returns>
    IReadOnlyList<string> GetAccessiblePages();

    /// <summary>
    /// Loads the current user's accessible page paths from the API and populates the in-memory cache.
    /// Called once per circuit during initialization (typically from the root layout or auth state handler).
    /// </summary>
    /// <returns>A task representing the asynchronous initialization operation.</returns>
    /// <remarks>
    /// <para>
    /// Calls GET <c>/api/page-permissions/my-pages</c> via the API service and stores the result
    /// in a case-insensitive <see cref="HashSet{T}"/> for O(1) subsequent lookups.
    /// </para>
    /// <para>
    /// If the API call fails, the cache is treated as empty and <see cref="CanAccess"/>
    /// returns <c>false</c> for all non-System_Pages.
    /// </para>
    /// </remarks>
    Task InitializeAsync();
}
