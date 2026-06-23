using AspireWebAppTemplate.Web.Abstractions;

namespace AspireWebAppTemplate.Web.Services;

/// <summary>
/// Per-circuit notification count cache that loads the authenticated user's unread count
/// once during circuit initialization and provides synchronous O(1) access for layout components.
/// </summary>
/// <remarks>
/// <para>
/// <b>Per-circuit caching strategy:</b> This service is registered as <b>scoped</b>, meaning each
/// Blazor Server SignalR circuit (user session) gets its own instance. The unread count is loaded
/// a single time via <see cref="InitializeAsync"/> and cached for the lifetime of the circuit.
/// Subsequent modifications (mark-as-read, dismiss) update the cache synchronously via
/// <see cref="DecrementCount"/> and <see cref="ClearCount"/> to avoid additional API round-trips
/// during page interactions.
/// </para>
/// <para>
/// <b>Pub/sub mechanism:</b> Layout components (NotificationBell) subscribe to <see cref="OnChange"/>
/// to re-render when the count changes. Page components (Notifications page) call
/// <see cref="DecrementCount"/>, <see cref="ClearCount"/>, or <see cref="RefreshAsync"/> after
/// performing mutations.
/// </para>
/// <para>
/// <b>Failure tolerance:</b> If the API call fails during initialization or refresh, the count
/// remains at 0 and a warning is logged. The service never throws exceptions to callers —
/// the notification badge simply shows no count rather than breaking the layout.
/// </para>
/// </remarks>
public sealed class NotificationContext(
    ApiNotificationService apiNotificationService,
    ILogger<NotificationContext> logger) : INotificationContext
{
    /// <summary>
    /// The cached unread notification count for the current user's circuit.
    /// </summary>
    private int _unreadCount;

    /// <summary>
    /// Tracks whether <see cref="InitializeAsync"/> has completed (success or failure).
    /// </summary>
    private bool _isLoaded;

    /// <inheritdoc />
    public int UnreadCount => _unreadCount;

    /// <inheritdoc />
    public bool IsLoaded => _isLoaded;

    /// <inheritdoc />
    public event Action? OnChange;

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Called once per circuit during initialization (typically from MainLayout.OnInitializedAsync).
    /// Makes a single API call to fetch the current unread count.
    /// </para>
    /// <para>
    /// On API failure (network error, non-success response), the count stays at 0 and
    /// <see cref="IsLoaded"/> is set to <c>true</c> so the UI transitions out of the loading state.
    /// A warning is logged to aid diagnostics.
    /// </para>
    /// </remarks>
    public async Task InitializeAsync()
    {
        try
        {
            var result = await apiNotificationService.GetUnreadCountAsync();

            if (result.Succeeded)
            {
                _unreadCount = result.Data;
            }
            else
            {
                // API returned a non-success result — keep count at 0 and log warning.
                logger.LogWarning(
                    "Failed to load unread notification count from API. Error: {Error}. " +
                    "Notification badge will show 0 until next refresh.",
                    result.Error);
                _unreadCount = 0;
            }
        }
        catch (Exception ex)
        {
            // Network failure or unexpected error — keep count at 0 and log warning.
            // The notification badge will simply not show a count rather than breaking the layout.
            logger.LogWarning(ex,
                "Exception occurred while loading unread notification count. " +
                "Notification badge will show 0 until next refresh.");
            _unreadCount = 0;
        }
        finally
        {
            // Always mark as loaded (even on failure) so the UI transitions out of
            // the loading state and doesn't leave components in a perpetual skeleton state
            _isLoaded = true;
            OnChange?.Invoke();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Clamps the result to zero using <see cref="Math.Max(int, int)"/> to prevent negative counts
    /// in edge cases where the cached count is stale (e.g., notification was already dismissed
    /// on another device).
    /// </remarks>
    public void DecrementCount(int amount = 1)
    {
        _unreadCount = Math.Max(0, _unreadCount - amount);
        OnChange?.Invoke();
    }

    /// <inheritdoc />
    public void ClearCount()
    {
        _unreadCount = 0;
        OnChange?.Invoke();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Makes a fresh API call to re-fetch the unread count. Used after operations where the
    /// exact delta is complex to compute locally (e.g., bulk dismiss of a mix of read/unread items).
    /// On API failure, the count is preserved at its current value and a warning is logged.
    /// </remarks>
    public async Task RefreshAsync()
    {
        try
        {
            var result = await apiNotificationService.GetUnreadCountAsync();

            if (result.Succeeded)
            {
                _unreadCount = result.Data;
            }
            else
            {
                // API failure during refresh — keep current count and log warning.
                // Better to show a slightly stale count than reset to 0 unexpectedly.
                logger.LogWarning(
                    "Failed to refresh unread notification count from API. Error: {Error}. " +
                    "Cached count remains at {CachedCount}.",
                    result.Error, _unreadCount);
            }
        }
        catch (Exception ex)
        {
            // Network failure during refresh — preserve current count and log warning.
            logger.LogWarning(ex,
                "Exception occurred while refreshing unread notification count. " +
                "Cached count remains at {CachedCount}.", _unreadCount);
        }
        finally
        {
            OnChange?.Invoke();
        }
    }
}
