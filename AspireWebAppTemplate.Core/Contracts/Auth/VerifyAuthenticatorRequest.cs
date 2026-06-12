namespace AspireWebAppTemplate.Core.Contracts.Auth;

/// <summary>
/// Request payload for verifying a TOTP authenticator code during 2FA setup.
/// </summary>
public sealed class VerifyAuthenticatorRequest
{
    public string Code { get; set; } = "";
}
