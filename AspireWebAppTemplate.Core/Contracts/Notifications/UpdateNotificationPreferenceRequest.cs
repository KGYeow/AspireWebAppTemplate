using AspireWebAppTemplate.Core.Domain.Enums;

namespace AspireWebAppTemplate.Core.Contracts.Notifications;

/// <summary>
/// Request DTO for updating a single notification delivery preference.
/// Persisted immediately using the instant-save pattern on the Settings page.
/// </summary>
public sealed class UpdateNotificationPreferenceRequest
{
    /// <summary>
    /// The notification category to update the preference for.
    /// </summary>
    public NotificationCategory Category { get; set; }

    /// <summary>
    /// Whether in-app notifications should be enabled for this category.
    /// </summary>
    public bool InAppEnabled { get; set; }

    /// <summary>
    /// Whether email notifications should be enabled for this category.
    /// </summary>
    public bool EmailEnabled { get; set; }
}
