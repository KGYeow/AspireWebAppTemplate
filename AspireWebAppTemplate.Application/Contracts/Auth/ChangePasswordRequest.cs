namespace AspireWebAppTemplate.Application.Contracts.Auth;

/// <summary>
/// Request payload for changing the current user's password.
/// Requires the current password for verification.
/// </summary>
public sealed class ChangePasswordRequest
{
    /// <summary>
    /// The user's current password for verification.
    /// </summary>
    public string CurrentPassword { get; set; } = "";

    /// <summary>
    /// The new password to set.
    /// </summary>
    public string NewPassword { get; set; } = "";
}
