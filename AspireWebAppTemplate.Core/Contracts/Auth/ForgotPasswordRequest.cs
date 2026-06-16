namespace AspireWebAppTemplate.Core.Contracts.Auth;

/// <summary>
/// Request payload for initiating a password reset via email.
/// </summary>
public sealed class ForgotPasswordRequest
{
    /// <summary>
    /// The email address associated with the account to reset.
    /// </summary>
    public string Email { get; set; } = "";
}
