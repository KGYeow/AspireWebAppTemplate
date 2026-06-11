namespace AspireWebAppTemplate.Core.Contracts;

/// <summary>
/// Contains the user's current external logins and whether they can be removed.
/// </summary>
public sealed class ExternalLoginsDto
{
    public List<ExternalLoginInfoDto> CurrentLogins { get; set; } = [];
    public bool ShowRemoveButton { get; set; }
}
