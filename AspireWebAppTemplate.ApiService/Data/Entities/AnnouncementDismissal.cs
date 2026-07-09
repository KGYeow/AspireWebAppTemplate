namespace AspireWebAppTemplate.ApiService.Data.Entities;

/// <summary>
/// Represents a per-user dismissal of a specific announcement.
/// Tracks when a user dismissed a banner announcement so it no longer appears in their view,
/// while remaining visible to other users who have not dismissed it.
/// </summary>
/// <remarks>
/// Uses a composite primary key of (UserId, AnnouncementId) — each user can dismiss
/// a given announcement at most once. Configured in <c>ApplicationDbContext.OnModelCreating</c> with:
/// <list type="bullet">
///   <item>Cascade delete from Announcement — removing an announcement cleans up all its dismissals.</item>
///   <item>Cascade delete from User — removing a user cleans up all their dismissal records.</item>
/// </list>
/// </remarks>
public class AnnouncementDismissal
{
    /// <summary>
    /// Gets or sets the identifier of the user who dismissed the announcement.
    /// </summary>
    /// <remarks>
    /// Part of the composite primary key. Foreign key referencing <c>ApplicationUser.Id</c>.
    /// Maximum length: 450 characters.
    /// </remarks>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the identifier of the dismissed announcement.
    /// </summary>
    /// <remarks>
    /// Part of the composite primary key. Foreign key referencing <c>Announcement.Id</c>.
    /// </remarks>
    public Guid AnnouncementId { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the user dismissed the announcement.
    /// </summary>
    /// <remarks>
    /// Set once at dismissal time. Used for auditing and potential future analytics
    /// (e.g., average time-to-dismiss).
    /// </remarks>
    public DateTime DismissedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the <see cref="ApplicationUser"/> who dismissed the announcement.
    /// </summary>
    /// <remarks>
    /// Configured with Cascade delete behavior so that removing a user from the system
    /// automatically removes all their dismissal records.
    /// </remarks>
    public ApplicationUser? User { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the dismissed <see cref="Announcement"/>.
    /// </summary>
    /// <remarks>
    /// Configured with Cascade delete behavior so that deleting an announcement
    /// automatically removes all associated dismissal records.
    /// </remarks>
    public Announcement? Announcement { get; set; }
}
