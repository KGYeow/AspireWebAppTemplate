using AspireWebAppTemplate.Domain.Entities.Template;
using AspireWebAppTemplate.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AspireWebAppTemplate.Infrastructure.Data.SeedData;

public static partial class SeedData
{
    #region Email Templates

    /// <summary>
    /// Seeds all email templates (system and business) for each <see cref="EmailType"/>. Uses an
    /// upsert pattern: inserts a template only if no existing record matches the <c>EmailType</c>.
    /// This preserves any administrator-customized templates across application restarts.
    /// </summary>
    /// <remarks>
    /// Seeds nine templates covering all <see cref="EmailType"/> values:
    /// <list type="bullet">
    ///   <item><see cref="EmailType.PasswordReset"/> — system, active.</item>
    ///   <item><see cref="EmailType.EmailConfirmation"/> — system, active.</item>
    ///   <item><see cref="EmailType.TwoFactorCode"/> — system, active.</item>
    ///   <item><see cref="EmailType.AccountLockout"/> — system, active.</item>
    ///   <item><see cref="EmailType.EmailChanged"/> — system, active.</item>
    ///   <item><see cref="EmailType.PasswordChanged"/> — system, active.</item>
    ///   <item><see cref="EmailType.WelcomeEmail"/> — business, active.</item>
    ///   <item><see cref="EmailType.AccountDeactivated"/> — business, inactive by default.</item>
    ///   <item><see cref="EmailType.CustomNotification"/> — business, active.</item>
    /// </list>
    /// </remarks>
    private static async Task SeedEmailTemplatesAsync(ApplicationDbContext dbContext, ILogger logger)
    {
        var utcNow = DateTime.UtcNow;

        var templates = new List<EmailTemplate>
        {
            // --- System security templates (read-only at runtime) ---

            new()
            {
                Id = Guid.NewGuid(),
                EmailType = EmailType.PasswordReset,
                DisplayName = "Password Reset",
                Subject = "Reset Your Password",
                HtmlBody = """
                    <!DOCTYPE html>
                    <html>
                    <body style="font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f5f5f5;">
                        <div style="max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 30px; border-radius: 8px;">
                            <h1 style="color: #1976d2; margin-top: 0;">Reset Your Password</h1>
                            <p style="color: #333; font-size: 16px; line-height: 1.6;">
                                Hello {{UserName}},
                            </p>
                            <p style="color: #333; font-size: 16px; line-height: 1.6;">
                                We received a request to reset your password. Click the button below to create a new password.
                            </p>
                            <div style="text-align: center; margin: 30px 0;">
                                <a href="{{ResetLink}}" style="background-color: #1976d2; color: #ffffff; padding: 14px 28px; text-decoration: none; border-radius: 4px; font-size: 16px; font-weight: bold; display: inline-block;">Reset Password</a>
                            </div>
                            <p style="color: #666; font-size: 14px; line-height: 1.6;">
                                If you did not request a password reset, please ignore this email. Do not share this link with anyone.
                            </p>
                            <p style="color: #999; font-size: 12px; margin-top: 20px; border-top: 1px solid #eee; padding-top: 15px;">
                                If the button above does not work, copy and paste the following URL into your browser:<br/>
                                <a href="{{ResetLink}}" style="color: #1976d2; word-break: break-all;">{{ResetLink}}</a>
                            </p>
                        </div>
                    </body>
                    </html>
                    """,
                Category = EmailTemplateCategory.System,
                IsActive = true,
                PlaceholderHints = "UserName,ResetLink",
                CreatedAtUtc = utcNow,
                UpdatedAtUtc = null
            },
            new()
            {
                Id = Guid.NewGuid(),
                EmailType = EmailType.EmailConfirmation,
                DisplayName = "Email Confirmation",
                Subject = "Confirm Your Email Address",
                HtmlBody = """
                    <!DOCTYPE html>
                    <html>
                    <body style="font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f5f5f5;">
                        <div style="max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 30px; border-radius: 8px;">
                            <h1 style="color: #1976d2; margin-top: 0;">Confirm Your Email</h1>
                            <p style="color: #333; font-size: 16px; line-height: 1.6;">
                                Hello {{UserName}},
                            </p>
                            <p style="color: #333; font-size: 16px; line-height: 1.6;">
                                Please confirm your email address by clicking the button below.
                            </p>
                            <div style="text-align: center; margin: 30px 0;">
                                <a href="{{ConfirmationLink}}" style="background-color: #1976d2; color: #ffffff; padding: 14px 28px; text-decoration: none; border-radius: 4px; font-size: 16px; font-weight: bold; display: inline-block;">Confirm Email</a>
                            </div>
                            <p style="color: #666; font-size: 14px; line-height: 1.6;">
                                If you did not create an account, please ignore this email.
                            </p>
                            <p style="color: #999; font-size: 12px; margin-top: 20px; border-top: 1px solid #eee; padding-top: 15px;">
                                If the button above does not work, copy and paste the following URL into your browser:<br/>
                                <a href="{{ConfirmationLink}}" style="color: #1976d2; word-break: break-all;">{{ConfirmationLink}}</a>
                            </p>
                        </div>
                    </body>
                    </html>
                    """,
                Category = EmailTemplateCategory.System,
                IsActive = true,
                PlaceholderHints = "UserName,ConfirmationLink",
                CreatedAtUtc = utcNow,
                UpdatedAtUtc = null
            },

            new()
            {
                Id = Guid.NewGuid(),
                EmailType = EmailType.TwoFactorCode,
                DisplayName = "Two-Factor Code",
                Subject = "Your Verification Code",
                HtmlBody = """
                    <!DOCTYPE html>
                    <html>
                    <body style="font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f5f5f5;">
                        <div style="max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 30px; border-radius: 8px;">
                            <h1 style="color: #1976d2; margin-top: 0;">Verification Code</h1>
                            <p style="color: #333; font-size: 16px; line-height: 1.6;">
                                Hello {{UserName}},
                            </p>
                            <p style="color: #333; font-size: 16px; line-height: 1.6;">
                                Your verification code is:
                            </p>
                            <div style="text-align: center; margin: 30px 0;">
                                <span style="background-color: #f5f5f5; border: 2px solid #1976d2; padding: 16px 32px; font-size: 28px; font-weight: bold; letter-spacing: 6px; border-radius: 4px; display: inline-block; color: #333;">{{TwoFactorCode}}</span>
                            </div>
                            <p style="color: #666; font-size: 14px; line-height: 1.6;">
                                This code will expire in 10 minutes. Do not share this code with anyone.
                            </p>
                            <p style="color: #999; font-size: 12px; margin-top: 20px; border-top: 1px solid #eee; padding-top: 15px;">
                                If you did not request this code, please secure your account immediately by changing your password.
                            </p>
                        </div>
                    </body>
                    </html>
                    """,
                Category = EmailTemplateCategory.System,
                IsActive = true,
                PlaceholderHints = "UserName,TwoFactorCode",
                CreatedAtUtc = utcNow,
                UpdatedAtUtc = null
            },
            new()
            {
                Id = Guid.NewGuid(),
                EmailType = EmailType.AccountLockout,
                DisplayName = "Account Lockout",
                Subject = "Account Locked",
                HtmlBody = """
                    <!DOCTYPE html>
                    <html>
                    <body style="font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f5f5f5;">
                        <div style="max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 30px; border-radius: 8px;">
                            <h1 style="color: #d32f2f; margin-top: 0;">Account Locked</h1>
                            <p style="color: #333; font-size: 16px; line-height: 1.6;">
                                Hello {{UserName}},
                            </p>
                            <p style="color: #333; font-size: 16px; line-height: 1.6;">
                                Your account has been temporarily locked due to multiple failed login attempts.
                            </p>
                            <p style="color: #333; font-size: 16px; line-height: 1.6;">
                                <strong>Lockout ends:</strong> {{LockoutEnd}}
                            </p>
                            <p style="color: #666; font-size: 14px; line-height: 1.6;">
                                If you did not attempt to log in, someone may be trying to access your account.
                                Please contact your system administrator if you need immediate assistance.
                            </p>
                            <p style="color: #999; font-size: 12px; margin-top: 20px; border-top: 1px solid #eee; padding-top: 15px;">
                                This is an automated security notification. Please do not reply to this email.
                            </p>
                        </div>
                    </body>
                    </html>
                    """,
                Category = EmailTemplateCategory.System,
                IsActive = true,
                PlaceholderHints = "UserName,LockoutEnd",
                CreatedAtUtc = utcNow,
                UpdatedAtUtc = null
            },

            new()
            {
                Id = Guid.NewGuid(),
                EmailType = EmailType.EmailChanged,
                DisplayName = "Email Changed",
                Subject = "Email Address Changed",
                HtmlBody = """
                    <!DOCTYPE html>
                    <html>
                    <body style="font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f5f5f5;">
                        <div style="max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 30px; border-radius: 8px;">
                            <h1 style="color: #1976d2; margin-top: 0;">Email Address Changed</h1>
                            <p style="color: #333; font-size: 16px; line-height: 1.6;">
                                Hello {{UserName}},
                            </p>
                            <p style="color: #333; font-size: 16px; line-height: 1.6;">
                                Your email address has been changed to <strong>{{NewEmail}}</strong>.
                                Please confirm your new email address by clicking the button below.
                            </p>
                            <div style="text-align: center; margin: 30px 0;">
                                <a href="{{ConfirmationLink}}" style="background-color: #1976d2; color: #ffffff; padding: 14px 28px; text-decoration: none; border-radius: 4px; font-size: 16px; font-weight: bold; display: inline-block;">Confirm New Email</a>
                            </div>
                            <p style="color: #666; font-size: 14px; line-height: 1.6;">
                                If you did not make this change, please contact your system administrator immediately.
                            </p>
                            <p style="color: #999; font-size: 12px; margin-top: 20px; border-top: 1px solid #eee; padding-top: 15px;">
                                If the button above does not work, copy and paste the following URL into your browser:<br/>
                                <a href="{{ConfirmationLink}}" style="color: #1976d2; word-break: break-all;">{{ConfirmationLink}}</a>
                            </p>
                        </div>
                    </body>
                    </html>
                    """,
                Category = EmailTemplateCategory.System,
                IsActive = true,
                PlaceholderHints = "UserName,NewEmail,ConfirmationLink",
                CreatedAtUtc = utcNow,
                UpdatedAtUtc = null
            },
            new()
            {
                Id = Guid.NewGuid(),
                EmailType = EmailType.PasswordChanged,
                DisplayName = "Password Changed",
                Subject = "Password Changed Successfully",
                HtmlBody = """
                    <!DOCTYPE html>
                    <html>
                    <body style="font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f5f5f5;">
                        <div style="max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 30px; border-radius: 8px;">
                            <h1 style="color: #388e3c; margin-top: 0;">Password Changed</h1>
                            <p style="color: #333; font-size: 16px; line-height: 1.6;">
                                Hello {{UserName}},
                            </p>
                            <p style="color: #333; font-size: 16px; line-height: 1.6;">
                                Your password has been changed successfully. If you made this change, no further action is required.
                            </p>
                            <p style="color: #d32f2f; font-size: 14px; line-height: 1.6; font-weight: bold;">
                                If you did not change your password, please contact your system administrator immediately
                                and reset your password as soon as possible.
                            </p>
                            <p style="color: #999; font-size: 12px; margin-top: 20px; border-top: 1px solid #eee; padding-top: 15px;">
                                This is an automated security notification. Please do not reply to this email.
                            </p>
                        </div>
                    </body>
                    </html>
                    """,
                Category = EmailTemplateCategory.System,
                IsActive = true,
                PlaceholderHints = "UserName",
                CreatedAtUtc = utcNow,
                UpdatedAtUtc = null
            },

            // --- Business notification templates (admin-editable at runtime) ---

            new()
            {
                Id = Guid.NewGuid(),
                EmailType = EmailType.WelcomeEmail,
                DisplayName = "Welcome Email",
                Subject = "Welcome, {{UserName}}!",
                HtmlBody = """
                    <!DOCTYPE html>
                    <html>
                    <body style="font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f5f5f5;">
                        <div style="max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 30px; border-radius: 8px;">
                            <h1 style="color: #1976d2; margin-top: 0;">Welcome, {{UserName}}!</h1>
                            <p style="color: #333; font-size: 16px; line-height: 1.6;">
                                We're glad to have you on board. Your account has been created successfully
                                and you're all set to get started.
                            </p>
                            <p style="color: #333; font-size: 16px; line-height: 1.6;">
                                Here are a few things you can do next:
                            </p>
                            <ul style="color: #555; font-size: 14px; line-height: 1.8;">
                                <li>Complete your profile in Account Settings</li>
                                <li>Explore the available features and modules</li>
                                <li>Set your notification preferences</li>
                            </ul>
                            <p style="color: #666; font-size: 14px; margin-top: 20px;">
                                If you have any questions, don't hesitate to reach out to the support team.
                            </p>
                        </div>
                    </body>
                    </html>
                    """,
                Category = EmailTemplateCategory.Business,
                IsActive = true,
                PlaceholderHints = "UserName",
                CreatedAtUtc = utcNow,
                UpdatedAtUtc = null
            },
            new()
            {
                Id = Guid.NewGuid(),
                EmailType = EmailType.AccountDeactivated,
                DisplayName = "Account Deactivated",
                Subject = "Your Account Has Been Deactivated",
                HtmlBody = """
                    <!DOCTYPE html>
                    <html>
                    <body style="font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f5f5f5;">
                        <div style="max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 30px; border-radius: 8px;">
                            <h1 style="color: #d32f2f; margin-top: 0;">Account Deactivated</h1>
                            <p style="color: #333; font-size: 16px; line-height: 1.6;">
                                Hello {{UserName}},
                            </p>
                            <p style="color: #333; font-size: 16px; line-height: 1.6;">
                                Your account has been deactivated by an administrator.
                            </p>
                            <p style="color: #333; font-size: 16px; line-height: 1.6;">
                                <strong>Reason:</strong> {{DeactivationReason}}
                            </p>
                            <p style="color: #666; font-size: 14px; margin-top: 20px;">
                                If you believe this was done in error, please contact your system administrator
                                for further assistance.
                            </p>
                        </div>
                    </body>
                    </html>
                    """,
                Category = EmailTemplateCategory.Business,
                IsActive = false,
                PlaceholderHints = "UserName,DeactivationReason",
                CreatedAtUtc = utcNow,
                UpdatedAtUtc = null
            },

            new()
            {
                Id = Guid.NewGuid(),
                EmailType = EmailType.CustomNotification,
                DisplayName = "Custom Notification",
                Subject = "{{Subject}}",
                HtmlBody = """
                    <!DOCTYPE html>
                    <html>
                    <body style="font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f5f5f5;">
                        <div style="max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 30px; border-radius: 8px;">
                            <p style="color: #333; font-size: 16px; line-height: 1.6;">
                                Hello {{UserName}},
                            </p>
                            <div style="color: #333; font-size: 16px; line-height: 1.6;">
                                {{Body}}
                            </div>
                            <p style="color: #666; font-size: 14px; margin-top: 20px; border-top: 1px solid #eee; padding-top: 15px;">
                                This is an automated notification. Please do not reply to this email.
                            </p>
                        </div>
                    </body>
                    </html>
                    """,
                Category = EmailTemplateCategory.Business,
                IsActive = true,
                PlaceholderHints = "UserName,Subject,Body",
                CreatedAtUtc = utcNow,
                UpdatedAtUtc = null
            }
        };

        var addedCount = 0;

        foreach (var template in templates)
        {
            // Upsert pattern: only insert if no existing record matches the EmailType.
            // This preserves any customizations made by administrators.
            var exists = await dbContext.EmailTemplates
                .AnyAsync(t => t.EmailType == template.EmailType);

            if (!exists)
            {
                dbContext.EmailTemplates.Add(template);
                addedCount++;
            }
        }

        if (addedCount > 0)
        {
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} email template(s).", addedCount);
        }
        else
        {
            logger.LogInformation("All email templates already seeded. Skipping.");
        }
    }

    #endregion
}
