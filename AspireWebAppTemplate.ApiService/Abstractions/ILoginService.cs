using AspireWebAppTemplate.Core.Contracts;
using AspireWebAppTemplate.Core.Contracts.Auth;
using AspireWebAppTemplate.Core.Contracts.AuditLog;
using AspireWebAppTemplate.Core.Contracts.Roles;
using AspireWebAppTemplate.Core.Contracts.Users;

namespace AspireWebAppTemplate.Abstractions;

/// <summary>
/// Defines the contract for the login service that handles credential validation
/// and token-based sign-in on a SignalR circuit (Interactive Server render mode).
/// </summary>
public interface ILoginService
{
    #region Operations

    /// <summary>
    /// Validates user credentials and, if successful, generates a single-use login token
    /// that can be redeemed at the <c>GET /Account/PerformLogin</c> endpoint.
    /// </summary>
    /// <param name="email">The user's email address.</param>
    /// <param name="password">The user's password.</param>
    /// <param name="rememberMe">Whether the authentication cookie should persist beyond the browser session.</param>
    /// <param name="returnUrl">The URL to redirect to after successful sign-in.</param>
    /// <returns>A <see cref="LoginResult"/> indicating the outcome of the validation.</returns>
    Task<LoginResult> ValidateAndGenerateTokenAsync(string email, string password, bool rememberMe, string returnUrl);

    #endregion
}
