namespace AspireWebAppTemplate.Domain.Enums;

/// <summary>
/// Classification of notification types for grouping and preference management.
/// Stored as a PascalCase string in the database via <c>HasConversion&lt;string&gt;()</c>.
/// </summary>
public enum NotificationCategory
{
    #region Template

    /// <summary>
    /// System-wide announcements such as maintenance windows, platform updates, and downtime notices.
    /// </summary>
    System,

    /// <summary>
    /// Account-related notifications such as password expiry reminders and login alerts.
    /// </summary>
    Account,

    /// <summary>
    /// Activity notifications such as task assignments, mentions, and workflow updates.
    /// </summary>
    Activity,

    #endregion

    #region Custom

    // Add your application-specific notification categories below this line.
    // Example:
    // Workflow,
    // Approval,
    // Billing,

    #endregion
}
