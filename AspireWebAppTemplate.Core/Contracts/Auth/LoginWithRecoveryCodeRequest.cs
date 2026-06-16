namespace AspireWebAppTemplate.Core.Contracts.Auth;

/// <summary>
/// Request payload for logging in with a recovery code when the authenticator app is unavailable.
/// </summary>
public sealed class LoginWithRecoveryCodeRequest
{
    /// <summary>
    /// One of the single-use recovery codes generated during 2FA setup.
    /// </summary>
    public string RecoveryCode { get; set; } = "";
}
