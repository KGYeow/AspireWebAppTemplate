namespace AspireWebAppTemplate.Core.Contracts.Auth;

/// <summary>
/// Request payload for changing the user's email address.
/// </summary>
public sealed class ChangeEmailRequest
{
    public string NewEmail { get; set; } = "";
}
