namespace AspireWebAppTemplate.Core.Contracts.Notifications;

/// <summary>
/// Request DTO for the internal notification callback from the API service to the Web project.
/// Contains the minimal data needed to deliver a real-time notification event via SignalR.
/// </summary>
public sealed class NotificationPushRequest
{
    /// <summary>
    /// The unique identifier of the target user (non-empty string).
    /// </summary>
    public string UserId { get; set; } = "";

    /// <summary>
    /// The notification title (non-empty, max 200 characters).
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// The notification category as a NotificationCategory string value.
    /// </summary>
    public string Category { get; set; } = "";

    /// <summary>
    /// The user's current total unread notification count (>= 0).
    /// </summary>
    public int UnreadCount { get; set; }

    /// <summary>
    /// The notification message body (may be empty).
    /// </summary>
    public string Message { get; set; } = "";
}
