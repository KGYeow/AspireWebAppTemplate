namespace AspireWebAppTemplate.Web.Abstractions;

/// <summary>
/// Provides per-circuit effective-permission state for Blazor Server under the resource-based
/// (<c>Module.Action</c>) authorization model. Loads the authenticated user's effective permission
/// keys once per circuit and exposes synchronous in-memory lookups for navigation and page
/// authorization checks.
/// </summary>
/// <remarks>
/// <para>
/// Registered as <b>scoped</b> — one instance per SignalR circuit (user session).
/// Effective permissions (the union of grants across all of the user's roles) are loaded via
/// <see cref="InitializeAsync"/> at circuit startup and cached in a case-insensitive
/// <see cref="HashSet{T}"/> for O(1) key lookups.
/// </para>
/// <para>
/// The Admin role is treated as having full access to every permission: when <see cref="IsAdmin"/>
/// is <c>true</c>, both <see cref="HasPermission(string)"/> and <see cref="HasAnyPermissionInModule(string)"/>
/// return <c>true</c> without consulting the cached set and without requiring the cache to be loaded.
/// </para>
/// <para>
/// The <c>PageAccessAuthorizationHandler</c> and <c>NavMenu</c> component consume this service to enforce
/// module-based page access and filter navigation items respectively.
/// </para>
/// </remarks>
public interface IPermissionContext
{
    /// <summary>
    /// Gets a value indicating whether the permission cache has been populated.
    /// Returns <c>true</c> after <see cref="InitializeAsync"/> completes (successfully or with error),
    /// <c>false</c> before initialization.
    /// </summary>
    /// <remarks>
    /// When <c>false</c>, <see cref="HasPermission(string)"/> and <see cref="HasAnyPermissionInModule(string)"/>
    /// return <c>false</c> for non-Admin users to prevent unauthorized access before permissions are loaded.
    /// Admin users short-circuit to <c>true</c> regardless of this flag.
    /// </remarks>
    bool IsLoaded { get; }

    /// <summary>
    /// Gets a value indicating whether the current user holds the Admin role.
    /// </summary>
    /// <remarks>
    /// When <c>true</c>, the user is treated as having full access to every permission and module,
    /// so permission and module checks succeed without consulting the cached permission set.
    /// </remarks>
    bool IsAdmin { get; }

    /// <summary>
    /// Determines whether the current user holds the specified permission key.
    /// </summary>
    /// <param name="permissionKey">
    /// The permission key to check, in <c>Module.Action</c> format (e.g. "Users.Read").
    /// Comparison is case-insensitive.
    /// </param>
    /// <returns>
    /// <c>true</c> if the current user is an Admin, or the key exists in the cached effective
    /// permission set; <c>false</c> if the key is not granted or the cache has not yet been loaded
    /// for a non-Admin user.
    /// </returns>
    bool HasPermission(string permissionKey);

    /// <summary>
    /// Determines whether the current user holds any permission belonging to the specified module.
    /// </summary>
    /// <param name="module">
    /// The module name to check (e.g. "Users"). A permission is considered part of the module when
    /// its key begins with <paramref name="module"/> followed by a dot (e.g. "Users."). Comparison is
    /// case-insensitive.
    /// </param>
    /// <returns>
    /// <c>true</c> if the current user is an Admin, or the cached effective permission set contains at
    /// least one key within the module; <c>false</c> if no such key is granted or the cache has not yet
    /// been loaded for a non-Admin user.
    /// </returns>
    bool HasAnyPermissionInModule(string module);

    /// <summary>
    /// Loads the current user's effective permission keys from the API and populates the in-memory cache.
    /// Called once per circuit during initialization (typically from the root layout or auth state handler).
    /// </summary>
    /// <returns>A task representing the asynchronous initialization operation.</returns>
    /// <remarks>
    /// <para>
    /// Calls GET <c>/api/permissions/my-permissions</c> via the API service and stores the result in a
    /// case-insensitive <see cref="HashSet{T}"/> for O(1) subsequent lookups.
    /// </para>
    /// <para>
    /// If the API call fails or the user is unauthenticated, the cache is treated as empty and the
    /// permission checks return <c>false</c> for non-Admin users.
    /// </para>
    /// </remarks>
    Task InitializeAsync();
}
