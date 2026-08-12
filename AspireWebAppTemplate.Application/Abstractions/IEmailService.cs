using AspireWebAppTemplate.Application.Contracts.Email;
using AspireWebAppTemplate.Domain.Enums;

namespace AspireWebAppTemplate.Application.Abstractions;

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
    /// <param name="request">The request containing email type, recipient, and template variables.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the SMTP client fails to connect, authenticate, or deliver the message.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no active template exists for the specified EmailType.
    /// </exception>
    Task SendEmailAsync(SendEmailRequest request);

    /// <summary>
    /// Attempts to send an email notification to the user, respecting their EmailEnabled preference
    /// for the specified notification category. This is best-effort: failures are logged but never
    /// propagated to the caller, ensuring email delivery does not disrupt primary operations.
    /// </summary>
    /// <param name="request">The request containing user ID, recipient, category, email type, and template variables.</param>
    /// <returns>A task representing the asynchronous operation. Never throws.</returns>
    Task TrySendEmailAsync(TrySendEmailRequest request);

    #endregion
}
