namespace AspireWebAppTemplate.Core.Contracts;

/// <summary>
/// Represents the current user's email information and confirmation status.
/// </summary>
public sealed class EmailInfoDto
{
    public string Email { get; set; } = "";
    public bool IsEmailConfirmed { get; set; }
}
