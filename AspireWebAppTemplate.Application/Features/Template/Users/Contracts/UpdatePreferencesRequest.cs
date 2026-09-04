using AspireWebAppTemplate.Domain.Enums;

namespace AspireWebAppTemplate.Application.Features.Template.Users;

/// <summary>
/// Request body for updating the current user's display preferences (theme, timezone, date format).
/// Sent from the preferences settings page to the API.
/// </summary>
public sealed class UpdatePreferencesRequest
{
    /// <summary>
    /// The user's preferred theme (Light, Dark, or System).
    /// </summary>
    public ThemePreference? Theme { get; set; }

    /// <summary>
    /// The IANA time zone identifier for the user's preferred timezone.
    /// </summary>
    public string? TimeZoneId { get; set; }

    /// <summary>
    /// The user's preferred date/time format string.
    /// </summary>
    public string? DateTimeFormat { get; set; }

    /// <summary>
    /// Whether real-time pop-up notifications are enabled for this user.
    /// Null means "no change" (partial update semantics).
    /// </summary>
    public bool? NotificationPopupsEnabled { get; set; }
}
