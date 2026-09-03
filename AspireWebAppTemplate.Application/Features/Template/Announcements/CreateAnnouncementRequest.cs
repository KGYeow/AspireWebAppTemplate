using AspireWebAppTemplate.Domain.Enums;

namespace AspireWebAppTemplate.Application.Features.Template.Announcements;

/// <summary>
/// Request DTO for creating a new announcement.
/// Submitted by administrators via the admin management page.
/// </summary>
public sealed class CreateAnnouncementRequest
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
    /// Whether the announcement is immediately active upon creation.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Whether user notifications are sent when the announcement becomes active.
    /// Defaults to true for Standard display type and false for Banner display type.
    /// </summary>
    public bool NotifyUsers { get; set; }
}
