using AspireWebAppTemplate.Core.Contracts;

namespace AspireWebAppTemplate.Abstractions;

/// <summary>
/// Defines the contract for low-level LDAP authentication and attribute retrieval.
/// </summary>
/// <remarks>
/// [LDAP] This interface is part of the LDAP integration. Remove it if LDAP is not needed.
/// </remarks>
public interface ILdapAuthService
{
    /// <summary>
    /// Authenticates a user against the corporate Active Directory and retrieves
    /// their attributes on success.
    /// </summary>
    /// <param name="identifier">The user's NTID (sAMAccountName) or email address.</param>
    /// <param name="password">The user's password.</param>
    /// <returns>An <see cref="LdapAuthResult"/> indicating the outcome.</returns>
    Task<LdapAuthResult> AuthenticateAsync(string identifier, string password);

    /// <summary>
    /// Fetches user attributes from Active Directory without authentication.
    /// Requires the application to run under a domain account with LDAP read permissions.
    /// </summary>
    /// <param name="identifier">The user's NTID (sAMAccountName) or email address.</param>
    /// <returns>The user's attributes, or <c>null</c> if not found.</returns>
    Task<LdapUserAttributes?> FetchUserAttributesAsync(string identifier);
}
