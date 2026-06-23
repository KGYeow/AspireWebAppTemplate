using AspireWebAppTemplate.Core.Domain.Enums;

namespace AspireWebAppTemplate.ApiService.Data.Entities;

/// <summary>
/// Represents a single in-app notification record for a user.
/// Each notification belongs to a <see cref="NotificationCategory"/> and tracks
/// whether it has been read by the recipient.
/// </summary>
/// <remarks>
/// Configured in <c>ApplicationDbContext.OnModelCreating</c> with composite indexes on
/// (UserId, IsRead) for efficient unread count queries and (UserId, CreatedAtUtc) for
/// efficient paginated retrieval in descending chronological order.
/// The foreign key to <see cref="ApplicationUser"/> uses cascade delete so that
/// removing a user automatically removes all their notification records.
/// </remarks>
public class Notification
{
    /// <summary>
    /// Gets or sets the unique identifier for this notification.
    /// </summary>
    /// <remarks>
    /// Primary key. Generated as a new <see cref="Guid"/> upon creation.
    /// </remarks>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user this notification belongs to.
    /// </summary>
    /// <remarks>
    /// Foreign key referencing <c>ApplicationUser.Id</c>. Maximum length: 450 characters.
    /// Part of composite indexes for efficient querying by user.
    /// </remarks>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the category classification for this notification.
    /// </summary>
    /// <remarks>
    /// Stored as a PascalCase string in the database via <c>HasConversion&lt;string&gt;()</c>
    /// for readability in raw SQL queries and to avoid breaking data if enum integer values shift.
    /// Used by the preference system to determine delivery channels.
    /// </remarks>
    public NotificationCategory Category { get; set; }

    /// <summary>
    /// Gets or sets the short title/subject line of the notification.
    /// </summary>
    /// <remarks>
    /// Maximum length: 256 characters. Displayed prominently in the notification bell
    /// dropdown and notification page list items.
    /// </remarks>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the detailed message body of the notification.
    /// </summary>
    /// <remarks>
    /// Maximum length: 1024 characters. Displayed as a preview in the notification
    /// page list items and in full when a notification is expanded.
    /// </remarks>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the notification has been read by the user.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>false</c> at the database level. Used in the composite index
    /// (UserId, IsRead) to efficiently count unread notifications per user.
    /// </remarks>
    public bool IsRead { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp indicating when the notification was created.
    /// </summary>
    /// <remarks>
    /// Always stored in UTC. The database column has a default value of <c>GETUTCDATE()</c>
    /// as a fallback, but the application explicitly sets this to <see cref="DateTime.UtcNow"/>
    /// at the time the notification is created by the service.
    /// Part of composite index (UserId, CreatedAtUtc) for efficient paginated retrieval.
    /// </remarks>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp indicating when the notification was marked as read.
    /// </summary>
    /// <remarks>
    /// Null when the notification has not been read. Set to the current UTC time when
    /// the user marks the notification as read. Once set, this value is not modified
    /// (mark-as-read is idempotent).
    /// </remarks>
    public DateTime? ReadAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the <see cref="ApplicationUser"/> who owns this notification.
    /// </summary>
    /// <remarks>
    /// Configured with cascade delete behavior so that removing a user from the system
    /// automatically removes all of their notification records.
    /// </remarks>
    public ApplicationUser? User { get; set; }
}
