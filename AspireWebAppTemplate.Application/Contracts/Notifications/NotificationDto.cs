using AspireWebAppTemplate.Domain.Enums;

namespace AspireWebAppTemplate.Application.Contracts.Notifications;

/// <summary>
/// Response DTO representing a single notification.
/// Returned by notification query and recent-notification endpoints.
/// </summary>
public sealed class NotificationDto
{
    /// <summary>
    /// The unique identifier of the notification.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The category classification of the notification.
    /// </summary>
    public NotificationCategory Category { get; set; }

    /// <summary>
    /// The short title summarizing the notification event.
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// The detailed message body of the notification.
    /// </summary>
    public string Message { get; set; } = "";

    /// <summary>
    /// Whether the notification has been marked as read by the user.
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// The UTC timestamp when the notification was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// The UTC timestamp when the notification was marked as read.
    /// Null if the notification has not been read.
    /// </summary>
    public DateTime? ReadAtUtc { get; set; }
}
