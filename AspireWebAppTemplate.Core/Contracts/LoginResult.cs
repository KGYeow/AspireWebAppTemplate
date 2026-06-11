namespace AspireWebAppTemplate.Core.Contracts;

/// <summary>
/// Represents the outcome of a login credential validation attempt.
/// </summary>
public sealed class LoginResult
{
    /// <summary>
    /// Indicates whether the credential validation succeeded and a token was generated.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Indicates whether the user's account requires two-factor authentication.
    /// </summary>
    public bool RequiresTwoFactor { get; init; }

    /// <summary>
    /// Indicates whether the user's account is currently locked out.
    /// </summary>
    public bool IsLockedOut { get; init; }

    /// <summary>
    /// Indicates whether the user's account is deactivated (<c>IsActive = false</c>).
    /// </summary>
    public bool IsDeactivated { get; init; }

    /// <summary>
    /// The single-use login token to be redeemed at the <c>GET /Account/PerformLogin</c> endpoint.
    /// Only populated when <see cref="Succeeded"/> is <c>true</c>.
    /// </summary>
    public string? Token { get; init; }

    /// <summary>
    /// The identifier of the authenticated user.
    /// Populated when <see cref="Succeeded"/> is <c>true</c>, used for audit logging.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// An error message to display to the user when validation fails.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
