using AspireWebAppTemplate.Core.Domain.Enums;

namespace AspireWebAppTemplate.ApiService.Abstractions;

/// <summary>
/// Defines the contract for sending emails via SMTP. Handles both system security emails
/// (triggered by ASP.NET Core Identity) and business notification emails (triggered by
/// application code). All templates are resolved from the database by EmailType.
/// </summary>
/// <remarks>
/// <para>
/// The implementation also satisfies <c>IEmailSender&lt;ApplicationUser&gt;</c> for Identity
/// integration. When SMTP configuration is missing or incomplete, the service falls back to
/// no-op behavior (logging email details without sending).
/// </para>
/// <para>
/// Registered as a scoped service to align with per-request DbContext lifetime.
/// </para>
/// </remarks>
public interface IEmailService
{
    #region Email Operations

    /// <summary>
    /// Sends an email for the specified <see cref="EmailType"/>. Resolves the template
    /// from the database via <see cref="IEmailTemplateService"/> and sends via SMTP.
    /// </summary>
    /// <param name="emailType">The email type to send.</param>
    /// <param name="recipientEmail">The recipient's email address.</param>
    /// <param name="variables">Dictionary of placeholder names to values for template rendering.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the SMTP client fails to connect, authenticate, or deliver the message.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no active template exists for the specified EmailType.
    /// </exception>
    Task SendEmailAsync(EmailType emailType, string recipientEmail, Dictionary<string, string> variables);

    /// <summary>
    /// Attempts to send an email notification to the user, respecting their EmailEnabled preference
    /// for the specified notification category. This is best-effort: failures are logged but never
    /// propagated to the caller, ensuring email delivery does not disrupt primary operations.
    /// </summary>
    /// <param name="userId">The target user's ID for preference lookup.</param>
    /// <param name="recipientEmail">The recipient's email address. Skipped if null or empty.</param>
    /// <param name="category">The notification category used to check the user's EmailEnabled preference.</param>
    /// <param name="emailType">The email type that determines which template is resolved from the database.</param>
    /// <param name="variables">Dictionary of placeholder names to values for template rendering.</param>
    /// <returns>A task representing the asynchronous operation. Never throws.</returns>
    Task TrySendEmailAsync(string userId, string? recipientEmail, NotificationCategory category, EmailType emailType, Dictionary<string, string> variables);

    #endregion
}
