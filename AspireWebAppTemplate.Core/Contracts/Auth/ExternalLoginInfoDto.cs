namespace AspireWebAppTemplate.Core.Contracts.Auth;

/// <summary>
/// Represents a linked external login provider for the current user.
/// </summary>
public sealed class ExternalLoginInfoDto
{
    /// <summary>
    /// The provider's unique identifier (e.g., "Google", "Microsoft").
    /// </summary>
    public string LoginProvider { get; set; } = "";

    /// <summary>
    /// The provider's human-readable display name.
    /// </summary>
    public string ProviderDisplayName { get; set; } = "";

    /// <summary>
    /// The provider-specific key identifying this user's account.
    /// </summary>
    public string ProviderKey { get; set; } = "";
}
