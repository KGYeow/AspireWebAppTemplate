using AspireWebAppTemplate.Web.Abstractions;
using Microsoft.AspNetCore.SignalR.Client;

namespace AspireWebAppTemplate.Web.Services;

/// <summary>
/// Per-circuit notification state manager that caches the unread count, manages the SignalR
/// hub connection lifecycle, and raises events for UI components to react to real-time updates.
/// </summary>
/// <remarks>
/// <para>
/// <b>Per-circuit caching strategy:</b> This service is registered as <b>scoped</b>, meaning each
/// Blazor Server SignalR circuit (user session) gets its own instance. The unread count is loaded
/// a single time via <see cref="InitializeAsync"/> and cached for the lifetime of the circuit.
/// Subsequent modifications (mark-as-read, dismiss) update the cache synchronously via
/// <see cref="DecrementCount"/> and <see cref="ClearCount"/> to avoid additional API round-trips.
/// </para>
/// <para>
/// <b>Hub connection ownership:</b> The SignalR hub connection to NotificationHub is created and
/// managed by this service. The connection is established during <see cref="InitializeAsync"/>
/// and disposed when the circuit ends. Reconnection uses exponential backoff via
/// <see cref="ExponentialBackoffRetryPolicy"/>.
/// </para>
/// <para>
/// <b>Event model:</b> <see cref="OnChange"/> fires on any unread count mutation (local or hub-driven).
/// <see cref="OnNotificationReceived"/> fires only when a new notification arrives via the hub,
/// providing title and category for UI-specific concerns (snackbar toast, dropdown prepend).
/// </para>
/// <para>
/// <b>Failure tolerance:</b> Hub connection failures are caught and logged. The service falls back
/// to navigation-based refresh when the hub is unavailable. No exceptions propagate to callers.
/// </para>
/// </remarks>
public sealed class NotificationContext : INotificationContext
{
    #region Constructor

    private readonly ApiNotificationService _apiNotificationService;
    private readonly ILogger<NotificationContext> _logger;

    /// <summary>
    /// The cached unread notification count for the current user's circuit.
    /// </summary>
    private int _unreadCount;

    /// <summary>
    /// Tracks whether <see cref="InitializeAsync"/> has completed (success or failure).
    /// </summary>
    private bool _isLoaded;

    /// <summary>
    /// The SignalR hub connection for receiving real-time notification events.
    /// Null before initialization or after disposal.
    /// </summary>
    private HubConnection? _hubConnection;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationContext"/> class.
    /// </summary>
    /// <param name="apiNotificationService">The typed HttpClient for notification API operations.</param>
    /// <param name="logger">The logger for recording warnings and errors.</param>
    public NotificationContext(ApiNotificationService apiNotificationService, ILogger<NotificationContext> logger)
    {
        _apiNotificationService = apiNotificationService;
        _logger = logger;
    }

    #endregion

    #region Properties and Events

    /// <inheritdoc />
    public int UnreadCount => _unreadCount;

    /// <inheritdoc />
    public bool IsLoaded => _isLoaded;

    /// <inheritdoc />
    public event Action? OnChange;

    /// <inheritdoc />
    public event Action<string, string>? OnNotificationReceived;

    #endregion

    #region Initialization

    /// <inheritdoc />
    public async Task InitializeAsync(Uri hubUrl)
    {
        // Load unread count from API.
        await LoadUnreadCountAsync();

        // Start the SignalR hub connection for real-time delivery.
        await StartHubConnectionAsync(hubUrl);
    }

    /// <summary>
    /// Loads the initial unread count from the API. On failure, count stays at 0.
    /// </summary>
    private async Task LoadUnreadCountAsync()
    {
        try
        {
            var result = await _apiNotificationService.GetUnreadCountAsync();

            if (result.Succeeded)
            {
                _unreadCount = result.Data;
            }
            else
            {
                // API returned a non-success result — keep count at 0 and log warning.
                _logger.LogWarning(
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
            _logger.LogWarning(ex,
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

    #endregion

    #region Hub Connection

    /// <summary>
    /// Establishes the SignalR hub connection with exponential backoff reconnection
    /// and registers event handlers for real-time notification delivery.
    /// </summary>
    private async Task StartHubConnectionAsync(Uri hubUrl)
    {
        try
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect(new ExponentialBackoffRetryPolicy())
                .Build();

            _hubConnection.On<string, string, int>("ReceiveNotification", HandleReceiveNotification);
            _hubConnection.Reconnected += HandleReconnected;
            _hubConnection.Closed += HandleClosed;

            await _hubConnection.StartAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to start NotificationHub connection. Falling back to navigation-based refresh.");
        }
    }

    /// <summary>
    /// Handles the "ReceiveNotification" event from the SignalR hub.
    /// Updates the cached unread count and raises events for UI components.
    /// </summary>
    private Task HandleReceiveNotification(string title, string category, int unreadCount)
    {
        _unreadCount = Math.Max(0, unreadCount);
        OnChange?.Invoke();
        OnNotificationReceived?.Invoke(title, category);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the hub reconnection event. Refreshes the unread count from the API
    /// to reconcile any notifications missed during disconnection.
    /// </summary>
    private async Task HandleReconnected(string? connectionId)
    {
        await RefreshAsync();
    }

    /// <summary>
    /// Handles the hub connection being permanently closed (all reconnection attempts exhausted).
    /// Logs a warning and falls back to navigation-based badge refresh.
    /// </summary>
    private Task HandleClosed(Exception? exception)
    {
        if (exception is not null)
        {
            _logger.LogWarning(exception,
                "NotificationHub connection closed permanently. Falling back to navigation-based refresh.");
        }

        return Task.CompletedTask;
    }

    #endregion

    #region Count Mutations

    /// <inheritdoc />
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
    public async Task RefreshAsync()
    {
        try
        {
            var result = await _apiNotificationService.GetUnreadCountAsync();

            if (result.Succeeded)
            {
                _unreadCount = result.Data;
            }
            else
            {
                // API failure during refresh — keep current count and log warning.
                // Better to show a slightly stale count than reset to 0 unexpectedly.
                _logger.LogWarning(
                    "Failed to refresh unread notification count from API. Error: {Error}. " +
                    "Cached count remains at {CachedCount}.",
                    result.Error, _unreadCount);
            }
        }
        catch (Exception ex)
        {
            // Network failure during refresh — preserve current count and log warning.
            _logger.LogWarning(ex,
                "Exception occurred while refreshing unread notification count. " +
                "Cached count remains at {CachedCount}.", _unreadCount);
        }
        finally
        {
            OnChange?.Invoke();
        }
    }

    /// <inheritdoc />
    public void UpdateFromHub(int unreadCount)
    {
        _unreadCount = Math.Max(0, unreadCount);
        OnChange?.Invoke();
    }

    #endregion

    #region Disposal

    /// <summary>
    /// Disposes the SignalR hub connection when the circuit ends.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            try
            {
                await _hubConnection.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing NotificationHub connection.");
            }

            _hubConnection = null;
        }
    }

    #endregion
}
