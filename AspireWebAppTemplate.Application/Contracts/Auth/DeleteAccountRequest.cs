namespace AspireWebAppTemplate.Application.Contracts.Auth;

/// <summary>
/// Request payload for deleting the user's account.
/// </summary>
public sealed class DeleteAccountRequest
{
    /// <summary>
    /// The user's current password, required to authorize account deletion.
    /// </summary>
    public string Password { get; set; } = "";
}
