namespace AspireWebAppTemplate.Application.Features.Template.Authentication;

/// <summary>
/// Represents a registered passkey (WebAuthn credential) for the current user.
/// </summary>
public sealed class PasskeyInfoDto
{
    /// <summary>
    /// The Base64-encoded credential ID of the passkey.
    /// </summary>
    public string CredentialId { get; set; } = "";

    /// <summary>
    /// The user-assigned friendly name for this passkey.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The UTC timestamp when this passkey was registered.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// The UTC timestamp when this passkey was last used for authentication.
    /// </summary>
    public DateTime? LastUsedUtc { get; set; }
}
