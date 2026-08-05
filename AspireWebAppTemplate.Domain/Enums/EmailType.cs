namespace AspireWebAppTemplate.Domain.Enums;

/// <summary>
/// Predefined types of emails sent by the application. Each type has exactly one template
/// in the database (seeded on first deployment). Application code references this enum to
/// send emails — the system resolves the template for that type from the database.
/// </summary>
/// <remarks>
/// This enum covers both system security emails and business notification emails.
/// The <see cref="EmailTemplateCategory"/> on the template entity determines whether
/// the template is read-only (System) or admin-editable (Business).
/// Adding new email types requires a code change, redeployment, and a new seed entry.
/// </remarks>
public enum EmailType
{
    // --- System security (read-only at runtime) ---

    /// <summary>
    /// Password reset email with a reset link.
    /// Typical placeholders: {{UserName}}, {{ResetLink}}.
    /// </summary>
    PasswordReset,

    /// <summary>
    /// Email address confirmation with a verification link.
    /// Typical placeholders: {{UserName}}, {{ConfirmationLink}}.
    /// </summary>
    EmailConfirmation,

    /// <summary>
    /// Two-factor authentication code delivery.
    /// Typical placeholders: {{UserName}}, {{TwoFactorCode}}.
    /// </summary>
    TwoFactorCode,

    /// <summary>
    /// Account lockout notification with lockout end time.
    /// Typical placeholders: {{UserName}}, {{LockoutEnd}}.
    /// </summary>
    AccountLockout,

    /// <summary>
    /// Email address change confirmation with a verification link.
    /// Typical placeholders: {{UserName}}, {{NewEmail}}, {{ConfirmationLink}}.
    /// </summary>
    EmailChanged,

    /// <summary>
    /// Password changed informational notification (no action link).
    /// Typical placeholders: {{UserName}}.
    /// </summary>
    PasswordChanged,

    // --- Business notifications (admin-editable at runtime) ---

    /// <summary>
    /// Welcome email sent to new users upon account creation or first login.
    /// Typical placeholders: {{UserName}}.
    /// </summary>
    WelcomeEmail,

    /// <summary>
    /// Notification sent when a user's account is deactivated by an administrator.
    /// Typical placeholders: {{UserName}}, {{DeactivationReason}}.
    /// </summary>
    AccountDeactivated,

    /// <summary>
    /// Generic custom notification template for ad-hoc business communications.
    /// Typical placeholders: {{UserName}}, {{Subject}}, {{Body}}.
    /// </summary>
    CustomNotification
}
