namespace AspireWebAppTemplate.Core.Contracts;

/// <summary>
/// Represents a linked external login provider for the current user.
/// </summary>
public sealed class ExternalLoginInfoDto
{
    public string LoginProvider { get; set; } = "";
    public string ProviderDisplayName { get; set; } = "";
    public string ProviderKey { get; set; } = "";
}
