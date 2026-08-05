namespace AspireWebAppTemplate.Application.Contracts.Auth;

/// <summary>
/// Contains the user's current external logins and whether they can be removed.
/// </summary>
public sealed class ExternalLoginsDto
{
    /// <summary>
    /// The list of currently linked external login providers.
    /// </summary>
    public List<ExternalLoginInfoDto> CurrentLogins { get; set; } = [];

    /// <summary>
    /// Whether the user can remove external logins (false if it's their only sign-in method).
    /// </summary>
    public bool ShowRemoveButton { get; set; }
}
