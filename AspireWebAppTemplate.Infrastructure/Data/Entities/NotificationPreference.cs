using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Domain.Enums;

namespace AspireWebAppTemplate.Infrastructure.Data.Entities;

/// <summary>
/// Represents a user's delivery preference for a specific notification category.
/// Controls which channels (in-app, email) are enabled for delivering notifications
/// of that category to the user.
/// </summary>
/// <remarks>
/// Configured in <c>ApplicationDbContext.OnModelCreating</c> with a unique composite index on
/// (UserId, Category) to enforce the invariant that at most one preference record exists
/// per user-category pair. The foreign key to <see cref="ApplicationUser"/> uses cascade delete
/// so that removing a user automatically removes all their preference records.
/// When no preference record exists for a user-category pair, the system treats defaults as
/// both <see cref="InAppEnabled"/> and <see cref="EmailEnabled"/> set to <c>true</c>.
/// </remarks>
public class NotificationPreference
{
    /// <summary>
    /// Gets or sets the unique identifier for this preference record.
    /// </summary>
    /// <remarks>
    /// Primary key. Generated as a new <see cref="Guid"/> upon creation.
    /// </remarks>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user this preference belongs to.
    /// </summary>
    /// <remarks>
    /// Foreign key referencing <c>ApplicationUser.Id</c>. Maximum length: 450 characters.
    /// Part of the unique composite index (UserId, Category) that enforces one preference
    /// per user-category pair.
    /// </remarks>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the notification category this preference applies to.
    /// </summary>
    /// <remarks>
    /// Stored as a PascalCase string in the database via <c>HasConversion&lt;string&gt;()</c>
    /// for readability in raw SQL queries and to avoid breaking data if enum integer values shift.
    /// Part of the unique composite index (UserId, Category).
    /// </remarks>
    public NotificationCategory Category { get; set; }

    /// <summary>
    /// Gets or sets whether in-app notifications are enabled for this category.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>true</c>. When <c>false</c>, the notification service skips creating
    /// in-app <see cref="Notification"/> entities for this user-category pair.
    /// </remarks>
    public bool InAppEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether email notifications are enabled for this category.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>true</c>. Reserved for future email notification support.
    /// Currently stored but not acted upon by the notification service.
    /// </remarks>
    public bool EmailEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the navigation property to the <see cref="ApplicationUser"/> who owns this preference.
    /// </summary>
    /// <remarks>
    /// Configured with cascade delete behavior so that removing a user from the system
    /// automatically removes all of their notification preference records.
    /// </remarks>
    public ApplicationUser? User { get; set; }
}
