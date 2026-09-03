using AspireWebAppTemplate.Domain.Enums;

namespace AspireWebAppTemplate.Application.Features.Template.Email;

/// <summary>
/// Request payload for attempting to send an email notification to a user,
/// respecting their per-category email preferences. Used by best-effort
/// email delivery that never throws on failure.
/// </summary>
public sealed class TrySendEmailRequest
{
    /// <summary>
    /// The target user's ID for preference lookup.
    /// </summary>
    public string UserId { get; set; } = "";

    /// <summary>
    /// The recipient's email address. Skipped if null or empty.
    /// </summary>
    public string? RecipientEmail { get; set; }

    /// <summary>
    /// The notification category used to check the user's EmailEnabled preference.
    /// </summary>
    public NotificationCategory Category { get; set; }

    /// <summary>
    /// The email type that determines which template is resolved from the database.
    /// </summary>
    public EmailType EmailType { get; set; }

    /// <summary>
    /// Dictionary of placeholder names to values for template rendering.
    /// </summary>
    public Dictionary<string, string> Variables { get; set; } = [];
}
