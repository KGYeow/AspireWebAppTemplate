namespace AspireWebAppTemplate.Core.Domain.Enums;

/// <summary>
/// Defines the types of entities that can be the subject of an audit log entry.
/// Stored as a PascalCase string in the database via <c>HasConversion&lt;string&gt;()</c>.
/// </summary>
public enum AuditEntityType
{
    #region Template

    /// <summary>
    /// The audited action relates to a user account (e.g., creation, update, authentication).
    /// </summary>
    User,

    /// <summary>
    /// The audited action relates to an application role (e.g., assignment, creation).
    /// </summary>
    Role,

    /// <summary>
    /// The audited action relates to application or user settings.
    /// </summary>
    Settings,

    /// <summary>
    /// The audited action relates to a system-level event not tied to a specific entity
    /// (e.g., failed login attempts where the user doesn't exist).
    /// </summary>
    System,

    /// <summary>
    /// The audited action relates to an announcement (e.g., creation, update, deletion).
    /// </summary>
    Announcement,

    #endregion

    #region Custom

    // Add your application-specific audit entity types below this line.
    // Example:
    // Order,
    // Invoice,
    // Customer,

    #endregion
}
