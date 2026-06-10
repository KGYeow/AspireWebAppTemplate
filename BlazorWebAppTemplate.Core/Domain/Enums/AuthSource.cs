namespace BlazorWebAppTemplate.Core.Domain.Enums;

/// <summary>
/// Identifies how a user account was created/authenticated.
/// Stored as a string in the database via <c>HasConversion&lt;string&gt;()</c>.
/// </summary>
public enum AuthSource
{
    /// <summary>
    /// User was created locally with email and password (default).
    /// </summary>
    Local,

    /// <summary>
    /// [LDAP] User was provisioned from the corporate Active Directory.
    /// </summary>
    LDAP
}
