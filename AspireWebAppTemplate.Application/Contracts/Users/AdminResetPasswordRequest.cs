namespace AspireWebAppTemplate.Application.Contracts.Users;

/// <summary>
/// Request DTO for an administrator resetting a user's password.
/// </summary>
public sealed class AdminResetPasswordRequest
{
    /// <summary>
    /// The new password to set for the user.
    /// Must comply with the application's password policy.
    /// </summary>
    public string NewPassword { get; set; } = "";
}
