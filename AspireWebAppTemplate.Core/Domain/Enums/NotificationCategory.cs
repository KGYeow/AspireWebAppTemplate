namespace AspireWebAppTemplate.Core.Domain.Enums;

/// <summary>
/// Classification of notification types for grouping and preference management.
/// Stored as a PascalCase string in the database via <c>HasConversion&lt;string&gt;()</c>.
/// </summary>
public enum NotificationCategory
{
    /// <summary>
    /// Security-related notifications such as password resets and login alerts.
    /// </summary>
    Security,

    /// <summary>
    /// User management notifications such as role changes and account activation/deactivation.
    /// </summary>
    UserManagement,

    /// <summary>
    /// System-level notifications such as maintenance windows and platform updates.
    /// </summary>
    System
}
