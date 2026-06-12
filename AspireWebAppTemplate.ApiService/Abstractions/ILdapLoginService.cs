using AspireWebAppTemplate.Core.Contracts;
using AspireWebAppTemplate.Core.Contracts.Auth;
using AspireWebAppTemplate.Core.Contracts.AuditLog;
using AspireWebAppTemplate.Core.Contracts.Roles;
using AspireWebAppTemplate.Core.Contracts.Users;

namespace AspireWebAppTemplate.Abstractions;

/// <summary>
/// Defines the contract for the LDAP login orchestration service that handles
/// LDAP authentication, auto-provisioning, attribute syncing, and token generation.
/// </summary>
/// <remarks>
/// [LDAP] This interface is part of the LDAP integration. Remove it if LDAP is not needed.
/// </remarks>
public interface ILdapLoginService
{
    /// <summary>
    /// Authenticates a user via LDAP, auto-provisions them in the local Identity
    /// database if needed, syncs their attributes, and generates a single-use login
    /// token for cookie sign-in.
    /// </summary>
    /// <param name="identifier">The user's NTID or email address.</param>
    /// <param name="password">The user's password.</param>
    /// <param name="rememberMe">Whether the authentication cookie should persist.</param>
    /// <param name="returnUrl">The URL to redirect to after sign-in.</param>
    /// <returns>A <see cref="LoginResult"/> indicating the outcome.</returns>
    Task<LoginResult> ValidateAndGenerateTokenAsync(string identifier, string password, bool rememberMe, string returnUrl);
}
