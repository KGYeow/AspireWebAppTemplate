namespace AspireWebAppTemplate.Domain.Enums;

/// <summary>
/// Indicates the urgency level of an announcement.
/// Affects banner styling, priority ordering, and visual indicators throughout the UI.
/// </summary>
public enum AnnouncementSeverity
{
    /// <summary>
    /// Informational announcement with no urgency. Displayed with blue/neutral styling.
    /// </summary>
    Info,

    /// <summary>
    /// Warning announcement requiring attention. Displayed with amber/orange styling.
    /// </summary>
    Warning,

    /// <summary>
    /// Critical announcement requiring immediate attention. Displayed with red styling.
    /// </summary>
    Critical
}
