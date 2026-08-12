using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Application.Contracts.Auth;
using AspireWebAppTemplate.Application.Contracts.AuditLog;
using AspireWebAppTemplate.Application.Contracts.Roles;
using AspireWebAppTemplate.Application.Contracts.Users;

namespace AspireWebAppTemplate.Application.Abstractions;

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
    /// <param name="request">The login request containing email, password, remember-me preference, and return URL.</param>
    /// <returns>A <see cref="LoginResult"/> indicating the outcome of the validation.</returns>
    Task<LoginResult> ValidateAndGenerateTokenAsync(LoginRequest request);

    #endregion
}
