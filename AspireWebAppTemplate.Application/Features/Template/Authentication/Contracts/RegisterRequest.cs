namespace AspireWebAppTemplate.Application.Features.Template.Authentication;

/// <summary>
/// Request payload for registering a new user account. Contains all parameters
/// needed for user creation, role assignment, and email confirmation setup.
/// </summary>
public sealed class RegisterRequest
{
    /// <summary>
    /// The user's email address (also used as the username).
    /// </summary>
    public string Email { get; set; } = "";

    /// <summary>
    /// The user's chosen password.
    /// </summary>
    public string Password { get; set; } = "";

    /// <summary>
    /// The absolute URI to the confirm-email page, used to construct
    /// the confirmation callback URL.
    /// </summary>
    public string ConfirmEmailBaseUri { get; set; } = "";

    /// <summary>
    /// Optional return URL passed through to the confirmation link.
    /// </summary>
    public string? ReturnUrl { get; set; }
}
