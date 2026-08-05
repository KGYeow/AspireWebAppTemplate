namespace AspireWebAppTemplate.Application.Contracts.Auth;

/// <summary>
/// Request payload for logging in with a two-factor authentication code.
/// </summary>
public sealed class LoginWith2faRequest
{
    /// <summary>
    /// The TOTP code from the user's authenticator app.
    /// </summary>
    public string Code { get; set; } = "";

    /// <summary>
    /// Whether the authentication cookie should persist beyond the browser session.
    /// </summary>
    public bool RememberMe { get; set; }

    /// <summary>
    /// Whether to suppress future 2FA prompts on this device.
    /// </summary>
    public bool RememberMachine { get; set; }
}
