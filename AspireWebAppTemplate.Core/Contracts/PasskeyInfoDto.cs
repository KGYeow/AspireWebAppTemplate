namespace AspireWebAppTemplate.Core.Contracts;

/// <summary>
/// Represents a registered passkey (WebAuthn credential) for the current user.
/// </summary>
public sealed class PasskeyInfoDto
{
    public string CredentialId { get; set; } = "";
    public string? Name { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? LastUsedUtc { get; set; }
}
