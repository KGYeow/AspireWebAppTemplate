using AspireWebAppTemplate.ApiService.Data.Entities;
using Microsoft.AspNetCore.Identity;

namespace AspireWebAppTemplate.ApiService.Services;

/// <summary>
/// No-op email sender that does nothing. Replace with a real implementation
/// (e.g., SendGrid, SMTP) when email functionality is needed.
/// </summary>
public sealed class NoOpEmailSender : IEmailSender<ApplicationUser>
{
    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
        => Task.CompletedTask;

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
        => Task.CompletedTask;

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
        => Task.CompletedTask;
}
