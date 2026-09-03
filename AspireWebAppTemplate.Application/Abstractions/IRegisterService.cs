using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Application.Contracts.Auth;
using AspireWebAppTemplate.Application.Features.Template.AuditLog.Contracts;
using AspireWebAppTemplate.Application.Contracts.Roles;
using AspireWebAppTemplate.Application.Contracts.Users;

namespace AspireWebAppTemplate.Application.Abstractions;

/// <summary>
/// Defines the contract for the registration service that handles user creation,
/// default role assignment, and email confirmation.
/// </summary>
public interface IRegisterService
{
    #region Operations

    /// <summary>
    /// Creates a new user account, assigns the default "User" role, generates an email
    /// confirmation token, and sends the confirmation email.
    /// </summary>
    /// <param name="request">The registration request containing email, password, confirmation URI, and optional return URL.</param>
    /// <returns>A <see cref="RegisterResult"/> indicating the outcome of the registration.</returns>
    Task<RegisterResult> RegisterUserAsync(RegisterRequest request);

    #endregion
}
