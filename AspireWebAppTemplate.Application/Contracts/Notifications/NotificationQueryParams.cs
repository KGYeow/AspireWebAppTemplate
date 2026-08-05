using AspireWebAppTemplate.Domain.Enums;

namespace AspireWebAppTemplate.Application.Contracts.Notifications;

/// <summary>
/// Query parameters for paginated notification retrieval.
/// Sent from the UI to the notification list endpoint.
/// </summary>
public sealed class NotificationQueryParams
{
    /// <summary>
    /// The one-based page index to retrieve. Defaults to 1.
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// The maximum number of notifications per page. Defaults to 20, maximum 100.
    /// </summary>
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Optional filter by notification category. When null, all categories are returned.
    /// </summary>
    public NotificationCategory? Category { get; set; }

    /// <summary>
    /// Optional filter by read status. When null, both read and unread notifications are returned.
    /// </summary>
    public bool? IsRead { get; set; }
}
