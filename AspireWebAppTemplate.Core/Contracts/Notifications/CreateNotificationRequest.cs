using AspireWebAppTemplate.Core.Domain.Enums;

namespace AspireWebAppTemplate.Core.Contracts.Notifications;

/// <summary>
/// Request DTO for creating a notification.
/// Used internally by backend services when significant events occur.
/// </summary>
public sealed class CreateNotificationRequest
{
    /// <summary>
    /// The ID of the user who should receive the notification.
    /// </summary>
    public string UserId { get; set; } = "";

    /// <summary>
    /// The category classification for the notification.
    /// </summary>
    public NotificationCategory Category { get; set; }

    /// <summary>
    /// The short title summarizing the notification event (max 256 characters).
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// The detailed message body of the notification (max 1024 characters).
    /// </summary>
    public string Message { get; set; } = "";
}
