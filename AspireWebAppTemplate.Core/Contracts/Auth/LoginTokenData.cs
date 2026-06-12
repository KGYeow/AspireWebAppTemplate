namespace AspireWebAppTemplate.Core.Contracts.Auth;

/// <summary>
/// Data stored in memory cache for the single-use login token.
/// Consumed by the GET /Account/PerformLogin endpoint.
/// </summary>
public sealed class LoginTokenData
{
    /// <summary>
    /// The ID of the authenticated user.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Whether the authentication cookie should persist beyond the browser session.
    /// </summary>
    public bool RememberMe { get; set; }

    /// <summary>
    /// The URL to redirect to after successful cookie sign-in.
    /// </summary>
    public string ReturnUrl { get; set; } = "/";
}
