using AspireWebAppTemplate.Core.Domain.Enums;

namespace AspireWebAppTemplate.Core.Contracts.Announcements;

/// <summary>
/// Response DTO representing a single announcement.
/// Returned by announcement query endpoints for both admin management and user-facing views.
/// </summary>
public sealed class AnnouncementDto
{
    /// <summary>
    /// The unique identifier of the announcement.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The plain-text title of the announcement (max 200 characters).
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The sanitized HTML content of the announcement authored via TinyMCE editor.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The display type controlling where the announcement is surfaced (Banner or Standard).
    /// </summary>
    public AnnouncementDisplayType DisplayType { get; set; }

    /// <summary>
    /// The severity level indicating announcement urgency (Info, Warning, Critical).
    /// </summary>
    public AnnouncementSeverity Severity { get; set; }

    /// <summary>
    /// The optional UTC timestamp when the announcement becomes active.
    /// Null means no start constraint (immediately active when IsActive is true).
    /// </summary>
    public DateTime? StartsAtUtc { get; set; }

    /// <summary>
    /// The optional UTC timestamp when the announcement expires.
    /// Null means no expiry constraint (active indefinitely when IsActive is true).
    /// </summary>
    public DateTime? ExpiresAtUtc { get; set; }

    /// <summary>
    /// Whether the announcement is manually activated by an administrator.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Whether user notifications are sent when the announcement becomes active.
    /// </summary>
    public bool NotifyUsers { get; set; }

    /// <summary>
    /// The computed status of the announcement based on IsActive, StartsAtUtc, and ExpiresAtUtc.
    /// Possible values: "Active", "Scheduled", "Expired", "Draft".
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// The UTC timestamp when the announcement was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// The UTC timestamp when the announcement was last updated.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>
    /// The display name of the administrator who created the announcement.
    /// Null if the creating user has been removed from the system.
    /// </summary>
    public string? CreatedByUserName { get; set; }
}
