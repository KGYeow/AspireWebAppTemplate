namespace AspireWebAppTemplate.Domain.Enums;

/// <summary>
/// Classification of email templates determining their editability at runtime.
/// Both categories are stored in the database with the same structure.
/// Stored as a PascalCase string in the database via <c>HasConversion&lt;string&gt;()</c>.
/// </summary>
public enum EmailTemplateCategory
{
    /// <summary>
    /// System security templates that are read-only at runtime.
    /// Administrators cannot modify these via the UI or API.
    /// Used for: password reset, email confirmation, 2FA code, account lockout, email changed, password changed.
    /// </summary>
    System,

    /// <summary>
    /// Business notification templates that are editable by administrators at runtime.
    /// One template per business EmailType — edit-only, no create or delete.
    /// Used for: welcome email, account deactivated, custom notifications.
    /// </summary>
    Business
}
