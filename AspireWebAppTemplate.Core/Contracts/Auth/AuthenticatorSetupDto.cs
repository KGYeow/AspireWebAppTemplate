namespace AspireWebAppTemplate.Core.Contracts.Auth;

/// <summary>
/// Contains the shared key and authenticator URI needed to set up a TOTP authenticator app.
/// </summary>
public sealed class AuthenticatorSetupDto
{
    public string SharedKey { get; set; } = "";
    public string AuthenticatorUri { get; set; } = "";
}
