namespace AspireWebAppTemplate.Core.Contracts.Auth;

/// <summary>
/// Request payload for renaming a passkey.
/// </summary>
public sealed class RenamePasskeyRequest
{
    public string Name { get; set; } = "";
}
