namespace AspireWebAppTemplate.Application.Features.Template.Authentication;

/// <summary>
/// Request payload for renaming a passkey's friendly name.
/// </summary>
public sealed class RenamePasskeyRequest
{
    /// <summary>
    /// The new friendly name for the passkey.
    /// </summary>
    public string Name { get; set; } = "";
}
