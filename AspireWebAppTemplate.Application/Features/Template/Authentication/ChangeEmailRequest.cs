namespace AspireWebAppTemplate.Application.Features.Template.Authentication;

/// <summary>
/// Request payload for changing the user's email address.
/// </summary>
public sealed class ChangeEmailRequest
{
    /// <summary>
    /// The new email address the user wants to change to.
    /// </summary>
    public string NewEmail { get; set; } = "";
}
