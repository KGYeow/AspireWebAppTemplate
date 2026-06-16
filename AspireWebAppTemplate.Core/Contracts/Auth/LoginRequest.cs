namespace AspireWebAppTemplate.Core.Contracts.Auth;

/// <summary>
/// Request payload for authenticating with email and password credentials.
/// </summary>
public sealed class LoginRequest
{
    /// <summary>
    /// The user's email address (used as username for login).
    /// </summary>
    public string Email { get; set; } = "";

    /// <summary>
    /// The user's password.
    /// </summary>
    public string Password { get; set; } = "";

    /// <summary>
    /// Whether the authentication cookie should persist beyond the browser session.
    /// </summary>
    public bool RememberMe { get; set; }

    /// <summary>
    /// Optional URL to redirect to after successful authentication.
    /// </summary>
    public string? ReturnUrl { get; set; }
}
