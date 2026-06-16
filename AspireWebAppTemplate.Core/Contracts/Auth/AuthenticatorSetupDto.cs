namespace AspireWebAppTemplate.Core.Contracts.Auth;

/// <summary>
/// Contains the shared key and authenticator URI needed to set up a TOTP authenticator app.
/// </summary>
public sealed class AuthenticatorSetupDto
{
    /// <summary>
    /// The Base32-encoded shared secret key for manual entry into the authenticator app.
    /// </summary>
    public string SharedKey { get; set; } = "";

    /// <summary>
    /// The otpauth:// URI used to generate a QR code for the authenticator app.
    /// </summary>
    public string AuthenticatorUri { get; set; } = "";
}
