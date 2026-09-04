namespace AspireWebAppTemplate.Application.Features.Template.Authentication;

/// <summary>
/// Request payload for resetting a user's password using a reset code from email.
/// </summary>
public sealed class ResetPasswordRequest
{
    /// <summary>
    /// The email address of the account whose password is being reset.
    /// </summary>
    public string Email { get; set; } = "";

    /// <summary>
    /// The password reset token received via email.
    /// </summary>
    public string Code { get; set; } = "";

    /// <summary>
    /// The new password to set for the account.
    /// </summary>
    public string NewPassword { get; set; } = "";
}
