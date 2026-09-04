namespace AspireWebAppTemplate.Application.Features.Template.Authentication;

/// <summary>
/// Represents the current user's two-factor authentication status.
/// </summary>
public sealed class TwoFactorStatusDto
{
    /// <summary>
    /// Whether the user has set up an authenticator app.
    /// </summary>
    public bool HasAuthenticator { get; set; }

    /// <summary>
    /// Whether two-factor authentication is currently enabled for the user.
    /// </summary>
    public bool Is2faEnabled { get; set; }

    /// <summary>
    /// Whether the current machine/browser is remembered (bypasses 2FA prompt).
    /// </summary>
    public bool IsMachineRemembered { get; set; }

    /// <summary>
    /// The number of unused recovery codes remaining.
    /// </summary>
    public int RecoveryCodesLeft { get; set; }
}
