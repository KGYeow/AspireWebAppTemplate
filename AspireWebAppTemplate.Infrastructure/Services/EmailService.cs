using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;
using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Application.Contracts.Email;
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Domain.Entities;
using AspireWebAppTemplate.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AspireWebAppTemplate.Infrastructure.Services;

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
    /// The application database context for querying notification preferences.
    /// </summary>
    private readonly ApplicationDbContext _dbContext;

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
    /// <param name="dbContext">The application database context for querying notification preferences.</param>
    /// <param name="configuration">The application configuration providing SMTP settings.</param>
    /// <param name="logger">The logger instance for email operation logging.</param>
    public EmailService(
        IEmailTemplateService templateService,
        ApplicationDbContext dbContext,
        IConfiguration configuration,
        ILogger<EmailService> logger)
    {
        _templateService = templateService;
        _dbContext = dbContext;
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
    public async Task SendEmailAsync(SendEmailRequest request)
    {
        var rendered = await _templateService.RenderAsync(request.EmailType, request.Variables);
        var maskedRecipient = MaskEmailAddress(request.RecipientEmail);

        if (!_isEnabled)
        {
            _logger.LogInformation(
                "Email send (no-op mode) — Template: {Template}, Recipient: {Recipient}, Subject: {Subject}",
                request.EmailType.ToString(), maskedRecipient, rendered.Subject);
            return;
        }

        try
        {
            using var smtpClient = CreateSmtpClient();
            using var mailMessage = new MailMessage
            {
                From = new MailAddress(_fromAddress, _fromName),
                Subject = rendered.Subject,
                Body = rendered.HtmlBody,
                IsBodyHtml = true
            };

            mailMessage.To.Add(new MailAddress(request.RecipientEmail));

            await smtpClient.SendMailAsync(mailMessage);

            _logger.LogInformation(
                "Email sent successfully — Template: {Template}, Recipient: {Recipient}",
                request.EmailType.ToString(), maskedRecipient);
        }
        catch (SmtpException ex)
        {
            _logger.LogError(ex,
                "SMTP error sending email — Template: {Template}, Recipient: {Recipient}, StatusCode: {StatusCode}",
                request.EmailType.ToString(), maskedRecipient, ex.StatusCode);

            throw new InvalidOperationException(
                $"Failed to send email via SMTP. Template: {request.EmailType}, Status: {ex.StatusCode}. {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task TrySendEmailAsync(TrySendEmailRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.RecipientEmail))
                return;

            // Check the user's email preference for this category.
            // If no preference record exists, default to EmailEnabled = true.
            var preference = await _dbContext.NotificationPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == request.UserId && p.Category == request.Category);

            var emailEnabled = preference?.EmailEnabled ?? true;
            if (!emailEnabled)
                return;

            await SendEmailAsync(new SendEmailRequest
            {
                EmailType = request.EmailType,
                RecipientEmail = request.RecipientEmail,
                Variables = request.Variables
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to send {EmailType} email to user '{UserId}'. Email delivery is best-effort.",
                request.EmailType,
                request.UserId);
        }
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

        await SendEmailAsync(new SendEmailRequest
        {
            EmailType = EmailType.EmailConfirmation,
            RecipientEmail = email,
            Variables = variables
        });
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

        await SendEmailAsync(new SendEmailRequest
        {
            EmailType = EmailType.PasswordReset,
            RecipientEmail = email,
            Variables = variables
        });
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

        await SendEmailAsync(new SendEmailRequest
        {
            EmailType = EmailType.TwoFactorCode,
            RecipientEmail = email,
            Variables = variables
        });
    }

    #endregion

    #region Private Helpers

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
