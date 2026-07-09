namespace AspireWebAppTemplate.Core.Domain.Enums;

/// <summary>
/// Classification of how an announcement is surfaced to users.
/// Stored as a PascalCase string in the database via <c>HasConversion&lt;string&gt;()</c>.
/// </summary>
public enum AnnouncementDisplayType
{
    /// <summary>
    /// Displayed in the persistent top-of-layout banner, dashboard card, and announcement list page.
    /// </summary>
    Banner,

    /// <summary>
    /// Displayed only in the dashboard card and announcement list page (not in the top banner).
    /// </summary>
    Standard
}
