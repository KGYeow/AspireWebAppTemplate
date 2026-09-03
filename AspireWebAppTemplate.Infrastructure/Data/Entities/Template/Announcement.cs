using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Domain.Enums;

namespace AspireWebAppTemplate.Infrastructure.Data.Entities.Template;

/// <summary>
/// Represents a system-wide announcement record that can be displayed to users
/// through multiple surfaces: a persistent top-of-layout banner, a dashboard card,
/// and a dedicated announcements list page.
/// </summary>
/// <remarks>
/// Configured in <c>ApplicationDbContext.OnModelCreating</c> with:
/// <list type="bullet">
///   <item>Composite index on (IsActive, StartsAtUtc, ExpiresAtUtc) for efficient active announcement queries.</item>
///   <item>Index on CreatedAtUtc for efficient ordering.</item>
///   <item>Restrict delete on CreatedByUser to preserve announcements even if the admin user is removed.</item>
///   <item>Cascade delete on Dismissals to automatically clean up dismissal records when an announcement is deleted.</item>
/// </list>
/// </remarks>
public class Announcement
{
    /// <summary>
    /// Gets or sets the unique identifier for this announcement.
    /// </summary>
    /// <remarks>
    /// Primary key. Generated as a new <see cref="Guid"/> upon creation.
    /// </remarks>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the plain-text title of the announcement.
    /// </summary>
    /// <remarks>
    /// Maximum length: 200 characters. Displayed in the banner, dashboard card,
    /// list page items, and admin grid.
    /// </remarks>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sanitized HTML content of the announcement.
    /// </summary>
    /// <remarks>
    /// Maximum length: 10000 characters. Authored via TinyMCE WYSIWYG editor and
    /// sanitized server-side using Ganss.Xss.HtmlSanitizer before persistence.
    /// Rendered as full HTML in the detail pane using MarkupString.
    /// Note: DTOs expose this as "Message" — the mapping occurs in the service layer.
    /// </remarks>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display type that controls where the announcement is surfaced.
    /// </summary>
    /// <remarks>
    /// Stored as a PascalCase string in the database via <c>HasConversion&lt;string&gt;()</c>.
    /// Banner type appears in the top-of-layout banner plus dashboard card and list page.
    /// Standard type appears only in the dashboard card and list page.
    /// </remarks>
    public AnnouncementDisplayType DisplayType { get; set; }

    /// <summary>
    /// Gets or sets the severity level indicating announcement urgency.
    /// </summary>
    /// <remarks>
    /// Stored as a PascalCase string in the database via <c>HasConversion&lt;string&gt;()</c>.
    /// Affects banner styling, priority ordering (Critical > Warning > Info),
    /// and visual indicators throughout the UI.
    /// </remarks>
    public AnnouncementSeverity Severity { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the announcement becomes visible.
    /// </summary>
    /// <remarks>
    /// Nullable — null means no start constraint (immediately active when IsActive is true).
    /// When set to a future date, the announcement is considered "Scheduled" until that time arrives.
    /// </remarks>
    public DateTime? StartsAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the announcement expires.
    /// </summary>
    /// <remarks>
    /// Nullable — null means no expiry (active indefinitely while IsActive is true).
    /// When the current UTC time is on or after this value, the announcement is considered "Expired"
    /// regardless of the IsActive flag.
    /// </remarks>
    public DateTime? ExpiresAtUtc { get; set; }

    /// <summary>
    /// Gets or sets whether the announcement is manually activated.
    /// </summary>
    /// <remarks>
    /// Default: false (Draft state). Combined with StartsAtUtc and ExpiresAtUtc to compute
    /// the effective status: Active, Scheduled, Expired, or Draft.
    /// </remarks>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets whether notifications should be sent to users when the announcement becomes active.
    /// </summary>
    /// <remarks>
    /// Default: false. When true and the announcement is immediately active,
    /// a notification is created for each active user in the system.
    /// </remarks>
    public bool NotifyUsers { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the administrator who created this announcement.
    /// </summary>
    /// <remarks>
    /// Foreign key referencing <c>ApplicationUser.Id</c>. Maximum length: 450 characters.
    /// Uses Restrict delete behavior to preserve the announcement even if the admin user is removed.
    /// </remarks>
    public string CreatedByUserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC timestamp when the announcement was created.
    /// </summary>
    /// <remarks>
    /// Set once at creation time. Used for ordering (newest first) and priority tie-breaking
    /// when announcements have the same severity level.
    /// </remarks>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the announcement was last updated.
    /// </summary>
    /// <remarks>
    /// Set on creation and refreshed on every subsequent update operation.
    /// </remarks>
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the <see cref="ApplicationUser"/> who created this announcement.
    /// </summary>
    /// <remarks>
    /// Configured with Restrict delete behavior so that removing an admin user from the system
    /// does not cascade-delete their announcements — announcements must be preserved for
    /// historical visibility even if the creator account is removed.
    /// </remarks>
    public ApplicationUser? CreatedByUser { get; set; }

    /// <summary>
    /// Gets or sets the collection of per-user dismissal records for this announcement.
    /// </summary>
    /// <remarks>
    /// Configured with Cascade delete behavior so that deleting an announcement
    /// automatically removes all associated dismissal records.
    /// </remarks>
    public ICollection<AnnouncementDismissal> Dismissals { get; set; } = new List<AnnouncementDismissal>();
}
