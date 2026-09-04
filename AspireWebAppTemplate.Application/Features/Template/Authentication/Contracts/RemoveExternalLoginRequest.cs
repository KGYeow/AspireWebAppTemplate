namespace AspireWebAppTemplate.Application.Features.Template.Authentication;

/// <summary>
/// Request payload for removing an external login provider from the user's account.
/// </summary>
public sealed class RemoveExternalLoginRequest
{
    /// <summary>
    /// The provider's unique identifier (e.g., "Google", "Microsoft").
    /// </summary>
    public string LoginProvider { get; set; } = "";

    /// <summary>
    /// The provider-specific key identifying this user's account.
    /// </summary>
    public string ProviderKey { get; set; } = "";
}
