namespace AspireWebAppTemplate.Application.Features.Template.Authentication;

/// <summary>
/// Request payload for confirming a user's email address via a confirmation link.
/// </summary>
public sealed class ConfirmEmailRequest
{
    /// <summary>
    /// The identifier of the user whose email is being confirmed.
    /// </summary>
    public string UserId { get; set; } = "";

    /// <summary>
    /// The email confirmation code (token) from the confirmation link.
    /// </summary>
    public string Code { get; set; } = "";
}
