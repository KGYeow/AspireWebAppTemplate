using AspireWebAppTemplate.Core.Domain.Enums;

namespace AspireWebAppTemplate.Core.Contracts.Notifications;

/// <summary>
/// Response DTO representing a user's notification delivery preference for a category.
/// Returned by the preferences endpoint for each notification category.
/// </summary>
public sealed class NotificationPreferenceDto
{
    /// <summary>
    /// The notification category this preference applies to.
    /// </summary>
    public NotificationCategory Category { get; set; }

    /// <summary>
    /// Whether in-app notifications are enabled for this category.
    /// </summary>
    public bool InAppEnabled { get; set; }

    /// <summary>
    /// Whether email notifications are enabled for this category.
    /// </summary>
    public bool EmailEnabled { get; set; }
}
