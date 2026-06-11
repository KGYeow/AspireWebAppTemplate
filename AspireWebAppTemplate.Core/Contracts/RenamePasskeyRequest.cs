namespace AspireWebAppTemplate.Core.Contracts;

/// <summary>
/// Request payload for renaming a passkey.
/// </summary>
public sealed class RenamePasskeyRequest
{
    public string Name { get; set; } = "";
}
