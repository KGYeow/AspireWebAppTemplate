namespace AspireWebAppTemplate.Core.Contracts.Auth;

/// <summary>
/// Request body for setting a local password on an account that doesn't have one yet
/// (e.g., accounts created via external login or LDAP).
/// </summary>
public sealed class SetPasswordRequest
{
    public string NewPassword { get; set; } = "";
}
