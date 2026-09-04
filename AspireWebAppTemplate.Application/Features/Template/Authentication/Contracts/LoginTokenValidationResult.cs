namespace AspireWebAppTemplate.Application.Features.Template.Authentication;

/// <summary>
/// Result of validating a single-use login token.
/// Contains the user claims needed to create an authentication cookie.
/// </summary>
public sealed class LoginTokenValidationResult
{
    /// <summary>
    /// The authenticated user's identifier.
    /// </summary>
    public string UserId { get; set; } = "";

    /// <summary>
    /// The authenticated user's username.
    /// </summary>
    public string UserName { get; set; } = "";

    /// <summary>
    /// The authenticated user's email address.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// The authenticated user's display name.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// The roles assigned to the authenticated user.
    /// </summary>
    public List<string> Roles { get; set; } = [];

    /// <summary>
    /// Whether the authentication cookie should persist beyond the browser session.
    /// </summary>
    public bool RememberMe { get; set; }

    /// <summary>
    /// The URL to redirect to after the cookie is set.
    /// </summary>
    public string? ReturnUrl { get; set; }
}
