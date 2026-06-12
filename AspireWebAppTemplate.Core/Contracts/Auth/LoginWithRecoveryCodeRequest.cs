namespace AspireWebAppTemplate.Core.Contracts.Auth;

/// <summary>
/// Request payload for logging in with a recovery code.
/// </summary>
public sealed class LoginWithRecoveryCodeRequest
{
    public string RecoveryCode { get; set; } = "";
}
