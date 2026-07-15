using System.Net;
using System.Net.Mail;
using AspireWebAppTemplate.ApiService.Abstractions;
using AspireWebAppTemplate.ApiService.Data.Entities;
using AspireWebAppTemplate.Core.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace AspireWebAppTemplate.ApiService.Services;

/// <summary>
/// Sends emails via SMTP using resolved templates from <see cref="IEmailTemplateService"/>.
/// Implements both the custom <see cref="IEmailService"/> interface and ASP.NET Core Identity's
/// <see cref="IEmailSender{TUser}"/> to replace <c>NoOpEmailSender</c>.
/// </summary>
/// <remarks>
/// <para>
/// SMTP configuration is read from the <c>Smtp</c> section in <c>appsettings.json</c> (host, port,
/// SSL, from address, from name) and Aspire environment variables (<c>Smtp__Username</c>,
/// <c>Smtp__Password</c>). When the host is missing or empty, the service falls back to no-op
/// behavior — logging email details without sending.
/// </para>
/// <para>
/// Credentials are applied only when both username and password are present and non-empty,
/// supporting relay-only SMTP configurations that require no authentication.
/// </para>
/// <para>
/// Registered as a scoped service to align with per-request DbContext lifetime.
/// </para>
/// </remarks>
public class EmailService : IEmailService, IEmailSender<ApplicationUser>
{
    #region Constructor

    /// <summary>
    /// The email template service used to resolve and render templates before sending.
    /// </summary>
    private readonly IEmailTemplateService _templateService;

    /// <summary>
    /// The application configuration providing SMTP settings from appsettings.json and environment variables.
    /// </summary>
    private readonly IConfiguration _configuration;

    /// <summary>
    /// The logger instance for recording email send operations and errors.
    /// </summary>
    private readonly ILogger<EmailService> _logger;

    /// <summary>
    /// The SMTP server hostname from configuration. Empty string indicates no-op mode.
    /// </summary>
    private readonly string _smtpHost;

    /// <summary>
    /// The SMTP server port from configuration. Defaults to 587 if not specified.
    /// </summary>
    private readonly int _smtpPort;

    /// <summary>
    /// Whether SSL/TLS is enabled for the SMTP connection.
    /// </summary>
    private readonly bool _enableSsl;

    /// <summary>
    /// The sender email address used as the "From" field on all outgoing emails.
    /// </summary>
    private readonly string _fromAddress;

    /// <summary>
    /// The sender display name shown alongside the "From" address.
    /// </summary>
    private readonly string _fromName;

    /// <summary>
    /// The SMTP authentication username from Aspire environment variables. Null or empty when not configured.
    /// </summary>
    private readonly string? _username;

    /// <summary>
    /// The SMTP authentication password from Aspire environment variables. Null or empty when not configured.
    /// </summary>
    private readonly string? _password;

    /// <summary>
    /// Indicates whether SMTP sending is enabled. False when host is missing or empty (no-op mode).
    /// </summary>
    private readonly bool _isEnabled;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailService"/> class.
    /// Reads SMTP configuration and logs a warning if the service falls back to no-op mode.
    /// </summary>
    /// <param name="templateService">The template service for resolving and rendering email templates.</param>
    /// <param name="configuration">The application configuration providing SMTP settings.</param>
    /// <param name="logger">The logger instance for email operation logging.</param>
    public EmailService(
        IEmailTemplateService templateService,
        IConfiguration configuration,
        ILogger<EmailService> logger)
    {
        _templateService = templateService;
        _configuration = configuration;
        _logger = logger;

        _smtpHost = _configuration["Smtp:Host"] ?? string.Empty;
        _smtpPort = _configuration.GetValue<int>("Smtp:Port", 587);
        _enableSsl = _configuration.GetValue<bool>("Smtp:EnableSsl", true);
        _fromAddress = _configuration["Smtp:FromAddress"] ?? "noreply@example.com";
        _fromName = _configuration["Smtp:FromName"] ?? "AspireWebApp";
        _username = _configuration["Smtp:Username"];
        _password = _configuration["Smtp:Password"];

        _isEnabled = !string.IsNullOrWhiteSpace(_smtpHost);

        if (!_isEnabled)
        {
            _logger.LogWarning("SMTP host is not configured. Email sending is disabled — emails will be logged but not sent.");
        }
    }

    #endregion

    #region Email Operations

    /// <inheritdoc />
    public async Task SendEmailAsync(EmailType emailType, string recipientEmail, Dictionary<string, string> variables)
    {
        var rendered = await _templateService.RenderAsync(emailType, variables);
        await SendEmailInternalAsync(recipientEmail, rendered.Subject, rendered.HtmlBody, emailType.ToString());
    }

    #endregion

    #region Identity Email Operations

    /// <summary>
    /// Sends an email confirmation link to the specified user. Delegates to
    /// <see cref="SendEmailAsync"/> with <see cref="EmailType.EmailConfirmation"/>.
    /// </summary>
    /// <param name="user">The application user to send the confirmation to.</param>
    /// <param name="email">The recipient email address.</param>
    /// <param name="confirmationLink">The URL the user must visit to confirm their email.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    public async Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        var variables = new Dictionary<string, string>
        {
            ["UserName"] = user.DisplayName ?? user.UserName ?? "User",
            ["ConfirmationLink"] = confirmationLink
        };

        await SendEmailAsync(EmailType.EmailConfirmation, email, variables);
    }

    /// <summary>
    /// Sends a password reset link to the specified user. Delegates to
    /// <see cref="SendEmailAsync"/> with <see cref="EmailType.PasswordReset"/>.
    /// </summary>
    /// <param name="user">The application user requesting the password reset.</param>
    /// <param name="email">The recipient email address.</param>
    /// <param name="resetLink">The URL the user must visit to reset their password.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    public async Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        var variables = new Dictionary<string, string>
        {
            ["UserName"] = user.DisplayName ?? user.UserName ?? "User",
            ["ResetLink"] = resetLink
        };

        await SendEmailAsync(EmailType.PasswordReset, email, variables);
    }

    /// <summary>
    /// Sends a password reset code (two-factor code) to the specified user. Delegates to
    /// <see cref="SendEmailAsync"/> with <see cref="EmailType.TwoFactorCode"/>.
    /// </summary>
    /// <param name="user">The application user requesting the code.</param>
    /// <param name="email">The recipient email address.</param>
    /// <param name="resetCode">The two-factor authentication code to include in the email.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    public async Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        var variables = new Dictionary<string, string>
        {
            ["UserName"] = user.DisplayName ?? user.UserName ?? "User",
            ["TwoFactorCode"] = resetCode
        };

        await SendEmailAsync(EmailType.TwoFactorCode, email, variables);
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Composes and sends an email message via SMTP, or logs the email details in no-op mode.
    /// Handles SMTP errors (connection, authentication, delivery) by logging at Error level
    /// and wrapping in <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <param name="recipientEmail">The recipient's email address.</param>
    /// <param name="subject">The email subject line.</param>
    /// <param name="htmlBody">The rendered HTML body content.</param>
    /// <param name="templateIdentifier">The template name or type used for logging purposes.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when SMTP connection, authentication, or delivery fails.
    /// </exception>
    private async Task SendEmailInternalAsync(string recipientEmail, string subject, string htmlBody, string templateIdentifier)
    {
        var maskedRecipient = MaskEmailAddress(recipientEmail);

        if (!_isEnabled)
        {
            _logger.LogInformation(
                "Email send (no-op mode) — Template: {Template}, Recipient: {Recipient}, Subject: {Subject}",
                templateIdentifier, maskedRecipient, subject);
            return;
        }

        try
        {
            using var smtpClient = CreateSmtpClient();
            using var mailMessage = new MailMessage
            {
                From = new MailAddress(_fromAddress, _fromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };

            mailMessage.To.Add(new MailAddress(recipientEmail));

            await smtpClient.SendMailAsync(mailMessage);

            _logger.LogInformation(
                "Email sent successfully — Template: {Template}, Recipient: {Recipient}",
                templateIdentifier, maskedRecipient);
        }
        catch (SmtpException ex)
        {
            _logger.LogError(ex,
                "SMTP error sending email — Template: {Template}, Recipient: {Recipient}, StatusCode: {StatusCode}",
                templateIdentifier, maskedRecipient, ex.StatusCode);

            throw new InvalidOperationException(
                $"Failed to send email via SMTP. Template: {templateIdentifier}, Status: {ex.StatusCode}. {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Creates and configures a new <see cref="SmtpClient"/> instance with the stored SMTP settings.
    /// Applies credentials only when both username and password are present and non-empty.
    /// </summary>
    /// <returns>A configured <see cref="SmtpClient"/> ready for sending.</returns>
    private SmtpClient CreateSmtpClient()
    {
        var client = new SmtpClient(_smtpHost, _smtpPort)
        {
            EnableSsl = _enableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(_username) && !string.IsNullOrWhiteSpace(_password))
        {
            client.Credentials = new NetworkCredential(_username, _password);
        }
        else
        {
            client.UseDefaultCredentials = false;
        }

        return client;
    }

    /// <summary>
    /// Masks an email address for logging purposes by showing only the first 3 characters
    /// of the local part followed by <c>***@domain</c>.
    /// For addresses with fewer than 3 characters in the local part, shows all available
    /// characters followed by the mask.
    /// </summary>
    /// <param name="email">The email address to mask.</param>
    /// <returns>The masked email address (e.g., "art***@example.com").</returns>
    private static string MaskEmailAddress(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return "***";

        var atIndex = email.IndexOf('@');
        if (atIndex <= 0)
            return "***";

        var localPart = email[..atIndex];
        var domain = email[atIndex..];
        var visibleChars = Math.Min(3, localPart.Length);

        return $"{localPart[..visibleChars]}***{domain}";
    }

    #endregion
}
