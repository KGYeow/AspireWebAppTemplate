namespace AspireWebAppTemplate.Application.Features.Template.Authentication;

/// <summary>
/// Request payload for verifying a TOTP authenticator code during 2FA setup.
/// </summary>
public sealed class VerifyAuthenticatorRequest
{
    /// <summary>
    /// The six-digit TOTP code from the user's authenticator app.
    /// </summary>
    public string Code { get; set; } = "";
}
