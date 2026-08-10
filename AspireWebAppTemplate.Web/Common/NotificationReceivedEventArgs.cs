namespace AspireWebAppTemplate.Web.Common;

/// <summary>
/// Event arguments raised when a new notification is received via the SignalR hub.
/// Bundles all notification event data for type-safe consumption by UI components.
/// </summary>
public sealed class NotificationReceivedEventArgs
{
    /// <summary>
    /// The notification title for display in UI components (snackbar, dropdown).
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The notification message body for display in UI components.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// The notification category as a string (e.g., "Account", "Activity", "System").
    /// Used for icon/color selection in UI components.
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// The unique identifier of the persisted notification entity.
    /// Used to construct deep-link URLs for direct navigation to the notification detail.
    /// </summary>
    public required Guid NotificationId { get; init; }
}
