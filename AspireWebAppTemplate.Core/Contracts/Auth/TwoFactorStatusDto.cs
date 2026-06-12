namespace AspireWebAppTemplate.Core.Contracts.Auth;

/// <summary>
/// Represents the current user's two-factor authentication status.
/// </summary>
public sealed class TwoFactorStatusDto
{
    public bool HasAuthenticator { get; set; }
    public bool Is2faEnabled { get; set; }
    public bool IsMachineRemembered { get; set; }
    public int RecoveryCodesLeft { get; set; }
}
