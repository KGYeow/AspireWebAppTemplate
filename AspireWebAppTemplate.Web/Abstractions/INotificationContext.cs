namespace AspireWebAppTemplate.Web.Abstractions;

/// <summary>
/// Per-circuit scoped service that manages real-time notification state and hub connectivity.
/// Provides synchronous O(1) access to the unread count for layout components and manages
/// the SignalR hub connection lifecycle for real-time notification delivery.
/// </summary>
/// <remarks>
/// <para>
/// Registered as <b>scoped</b> — one instance per SignalR circuit (user session).
/// The unread count is loaded once via <see cref="InitializeAsync"/> at circuit startup
/// and subsequently updated synchronously by page components after mark-as-read or dismiss actions,
/// or via the SignalR hub when new notifications arrive.
/// </para>
/// <para>
/// Subscribers (e.g., NotificationBell in the layout) listen to <see cref="OnChange"/> and call
/// <c>StateHasChanged</c> to re-render when the count changes. They listen to
/// <see cref="OnNotificationReceived"/> for UI-specific reactions (toast, dropdown update).
/// </para>
/// </remarks>
public interface INotificationContext : IAsyncDisposable
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
    /// Gets or sets whether pop-up notifications are enabled for this user.
    /// Shared per-circuit so that settings changes are immediately reflected in the layout.
    /// </summary>
    bool NotificationPopupsEnabled { get; set; }

    /// <summary>
    /// Raised when <see cref="UnreadCount"/> changes. Subscribers should call <c>StateHasChanged</c>
    /// to re-render with the updated count.
    /// </summary>
    event Action? OnChange;

    /// <summary>
    /// Raised when a new notification arrives via the SignalR hub. Provides the notification
    /// title, message, and category for UI-specific reactions (snackbar toast, dropdown update).
    /// </summary>
    /// <remarks>
    /// This event is raised after <see cref="OnChange"/> — the unread count is already updated
    /// when this fires. Subscribers use this for UI-only concerns (toast display, list prepending).
    /// </remarks>
    event Action<string, string, string>? OnNotificationReceived;

    /// <summary>
    /// Loads the unread count from the API and starts the SignalR hub connection.
    /// Called once per circuit during initialization (typically from the root layout or
    /// the first component that needs notification state).
    /// </summary>
    /// <param name="hubUrl">The absolute URL for the NotificationHub endpoint.</param>
    /// <returns>A task representing the asynchronous initialization operation.</returns>
    /// <remarks>
    /// On API failure, <see cref="UnreadCount"/> remains at 0, <see cref="IsLoaded"/> is set to
    /// <c>true</c>, and a warning is logged. Hub connection failures are handled gracefully
    /// with fallback to navigation-based refresh.
    /// </remarks>
    Task InitializeAsync(Uri hubUrl);

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

    /// <summary>
    /// Replaces the cached unread count with the server-authoritative value received
    /// from the NotificationHub. Raises <see cref="OnChange"/> to trigger UI re-renders.
    /// </summary>
    /// <param name="unreadCount">The authoritative unread count from the server.</param>
    void UpdateFromHub(int unreadCount);
}
