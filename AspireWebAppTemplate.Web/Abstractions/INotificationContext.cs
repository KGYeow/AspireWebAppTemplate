namespace AspireWebAppTemplate.Web.Abstractions;

/// <summary>
/// Per-circuit scoped service caching the current user's unread notification count.
/// Provides synchronous O(1) access for layout components (NotificationBell) without
/// requiring async calls on every render.
/// </summary>
/// <remarks>
/// <para>
/// Registered as <b>scoped</b> — one instance per SignalR circuit (user session).
/// The unread count is loaded once via <see cref="InitializeAsync"/> at circuit startup
/// and subsequently updated synchronously by page components after mark-as-read or dismiss actions.
/// </para>
/// <para>
/// Subscribers (e.g., NotificationBell in the layout) listen to <see cref="OnChange"/> and call
/// <c>StateHasChanged</c> to re-render when the count changes.
/// </para>
/// </remarks>
public interface INotificationContext
{
    /// <summary>
    /// Gets the cached unread notification count. Returns 0 before initialization completes.
    /// </summary>
    int UnreadCount { get; }

    /// <summary>
    /// Gets whether the context has been initialized (loaded from API).
    /// Returns <c>true</c> after <see cref="InitializeAsync"/> completes (successfully or with error),
    /// <c>false</c> before initialization.
    /// </summary>
    bool IsLoaded { get; }

    /// <summary>
    /// Raised when <see cref="UnreadCount"/> changes. Subscribers should call <c>StateHasChanged</c>
    /// to re-render with the updated count.
    /// </summary>
    event Action? OnChange;

    /// <summary>
    /// Loads the unread count from the API. Called once per circuit during initialization
    /// (typically from the root layout's <c>OnInitializedAsync</c>).
    /// </summary>
    /// <returns>A task representing the asynchronous initialization operation.</returns>
    /// <remarks>
    /// On API failure, <see cref="UnreadCount"/> remains at 0, <see cref="IsLoaded"/> is set to
    /// <c>true</c>, and a warning is logged. The application does not remain in a perpetual
    /// loading state.
    /// </remarks>
    Task InitializeAsync();

    /// <summary>
    /// Decrements the cached unread count by the specified amount (e.g., after marking a notification as read).
    /// Clamps to zero to prevent negative values.
    /// </summary>
    /// <param name="amount">The number to subtract from the current count. Defaults to 1.</param>
    void DecrementCount(int amount = 1);

    /// <summary>
    /// Sets the cached unread count to zero (e.g., after a mark-all-as-read operation).
    /// </summary>
    void ClearCount();

    /// <summary>
    /// Reloads the unread count from the API (e.g., after bulk dismiss where the exact delta is complex).
    /// Updates the cache and raises <see cref="OnChange"/>.
    /// </summary>
    /// <returns>A task representing the asynchronous refresh operation.</returns>
    Task RefreshAsync();
}
