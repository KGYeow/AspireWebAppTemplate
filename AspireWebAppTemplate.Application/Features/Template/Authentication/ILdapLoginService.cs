using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Application.Features.Template.Authentication;
using AspireWebAppTemplate.Application.Features.Template.AuditLog;
using AspireWebAppTemplate.Application.Features.Template.Roles;
using AspireWebAppTemplate.Application.Features.Template.Users;

namespace AspireWebAppTemplate.Application.Features.Template.Authentication;

/// <summary>
/// Defines the contract for the LDAP login orchestration service that handles
/// LDAP authentication, auto-provisioning, attribute syncing, and token generation.
/// </summary>
/// <remarks>
/// [LDAP] This interface is part of the LDAP integration. Remove it if LDAP is not needed.
/// </remarks>
public interface ILdapLoginService
{
    #region Operations

    /// <summary>
    /// Authenticates a user via LDAP, auto-provisions them in the local Identity
    /// database if needed, syncs their attributes, and generates a single-use login
    /// token for cookie sign-in.
    /// </summary>
    /// <param name="request">
    /// The login request containing the user's NTID or email (via <see cref="LoginRequest.Email"/>),
    /// password, remember-me preference, and optional return URL.
    /// </param>
    /// <returns>A <see cref="LoginResult"/> indicating the outcome.</returns>
    Task<LoginResult> ValidateAndGenerateTokenAsync(LoginRequest request);

    #endregion
}
