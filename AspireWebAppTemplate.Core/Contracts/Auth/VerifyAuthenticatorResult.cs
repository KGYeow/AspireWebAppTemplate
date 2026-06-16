namespace AspireWebAppTemplate.Core.Contracts.Auth;

/// <summary>
/// Result of verifying a TOTP authenticator code. Contains recovery codes on success.
/// </summary>
public sealed class VerifyAuthenticatorResult
{
    /// <summary>
    /// Whether the TOTP code verification succeeded and 2FA is now enabled.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// The generated recovery codes, only populated when <see cref="Succeeded"/> is <c>true</c>.
    /// </summary>
    public string[]? RecoveryCodes { get; set; }
}
