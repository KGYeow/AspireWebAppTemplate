namespace AspireWebAppTemplate.Core.Contracts;

/// <summary>
/// Result of verifying a TOTP authenticator code. Contains recovery codes on success.
/// </summary>
public sealed class VerifyAuthenticatorResult
{
    public bool Succeeded { get; set; }
    public string[]? RecoveryCodes { get; set; }
}
