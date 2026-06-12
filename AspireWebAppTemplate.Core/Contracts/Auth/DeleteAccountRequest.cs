namespace AspireWebAppTemplate.Core.Contracts.Auth;

/// <summary>
/// Request payload for deleting the user's account.
/// </summary>
public sealed class DeleteAccountRequest
{
    public string Password { get; set; } = "";
}
