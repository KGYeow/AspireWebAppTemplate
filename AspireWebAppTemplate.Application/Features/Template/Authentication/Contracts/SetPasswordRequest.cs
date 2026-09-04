namespace AspireWebAppTemplate.Application.Features.Template.Authentication;

/// <summary>
/// Request body for setting a local password on an account that doesn't have one yet
/// (e.g., accounts created via external login or LDAP).
/// </summary>
public sealed class SetPasswordRequest
{
    /// <summary>
    /// The new local password to set for the account.
    /// </summary>
    public string NewPassword { get; set; } = "";
}
