namespace AspireWebAppTemplate.Core.Contracts;

/// <summary>
/// User attributes retrieved from Active Directory.
/// </summary>
/// <remarks>
/// [LDAP] This class is part of the LDAP integration. Remove it if LDAP is not needed.
/// </remarks>
public sealed class LdapUserAttributes
{
    /// <summary>
    /// The user's display name from AD (displayName).
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// The user's first/given name from AD (givenName).
    /// </summary>
    public string FirstName { get; init; } = string.Empty;

    /// <summary>
    /// The user's last/family name from AD (sn).
    /// </summary>
    public string LastName { get; init; } = string.Empty;

    /// <summary>
    /// The user's job title from AD (title).
    /// </summary>
    public string JobTitle { get; init; } = string.Empty;

    /// <summary>
    /// The user's department from AD (department).
    /// </summary>
    public string Department { get; init; } = string.Empty;

    /// <summary>
    /// The user's email address from AD (mail).
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// The user's sAMAccountName (NTID) from AD (samaccountname).
    /// </summary>
    public string Ntid { get; init; } = string.Empty;

    /// <summary>
    /// The user's employee number from AD (employeeNumber).
    /// </summary>
    public string EmployeeNumber { get; init; } = string.Empty;
}
