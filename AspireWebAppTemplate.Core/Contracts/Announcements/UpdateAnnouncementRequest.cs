using AspireWebAppTemplate.Core.Domain.Enums;

namespace AspireWebAppTemplate.Core.Contracts.Announcements;

/// <summary>
/// Request DTO for updating an existing announcement.
/// Submitted by administrators via the admin management page.
/// </summary>
public sealed class UpdateAnnouncementRequest
{
    /// <summary>
    /// The plain-text title of the announcement (required, max 200 characters).
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The HTML content of the announcement authored via TinyMCE editor (required, max 10000 characters).
    /// Content is sanitized server-side before persistence.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The display type controlling where the announcement is surfaced.
    /// </summary>
    public AnnouncementDisplayType DisplayType { get; set; }

    /// <summary>
    /// The severity level indicating announcement urgency.
    /// </summary>
    public AnnouncementSeverity Severity { get; set; }

    /// <summary>
    /// The optional UTC timestamp when the announcement becomes active.
    /// Null means no start constraint.
    /// </summary>
    public DateTime? StartsAtUtc { get; set; }

    /// <summary>
    /// The optional UTC timestamp when the announcement expires.
    /// Null means no expiry constraint.
    /// </summary>
    public DateTime? ExpiresAtUtc { get; set; }

    /// <summary>
    /// Whether the announcement is active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Whether user notifications are sent for this update.
    /// This is a per-request flag (not persisted) — controls whether to notify users about this specific edit.
    /// </summary>
    public bool NotifyUsers { get; set; }

    /// <summary>
    /// Whether to clear all existing dismissal records for this announcement.
    /// When true, all users will see the updated announcement again (including in the banner).
    /// </summary>
    public bool ClearDismissals { get; set; }
}
