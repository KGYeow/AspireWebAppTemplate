namespace AspireWebAppTemplate.Core.Contracts.Auth;

/// <summary>
/// Represents the current user's email information and confirmation status.
/// </summary>
public sealed class EmailInfoDto
{
    /// <summary>
    /// The user's current email address.
    /// </summary>
    public string Email { get; set; } = "";

    /// <summary>
    /// Whether the user's email address has been confirmed.
    /// </summary>
    public bool IsEmailConfirmed { get; set; }
}
