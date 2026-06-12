namespace AspireWebAppTemplate.Core.Contracts.Auth;

/// <summary>
/// Request payload for removing an external login provider.
/// </summary>
public sealed class RemoveExternalLoginRequest
{
    public string LoginProvider { get; set; } = "";
    public string ProviderKey { get; set; } = "";
}
