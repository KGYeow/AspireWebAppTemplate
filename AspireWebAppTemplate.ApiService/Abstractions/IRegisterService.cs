using BlazorWebAppTemplate.Contracts;

namespace BlazorWebAppTemplate.Abstractions;

/// <summary>
/// Defines the contract for the registration service that handles user creation,
/// default role assignment, and email confirmation.
/// </summary>
public interface IRegisterService
{
    /// <summary>
    /// Creates a new user account, assigns the default "User" role, generates an email
    /// confirmation token, and sends the confirmation email.
    /// </summary>
    /// <param name="email">The user's email address (also used as the username).</param>
    /// <param name="password">The user's chosen password.</param>
    /// <param name="confirmEmailBaseUri">
    /// The absolute URI to the <c>Account/ConfirmEmail</c> page, used to construct the
    /// confirmation callback URL.
    /// </param>
    /// <param name="returnUrl">Optional return URL passed through to the confirmation link.</param>
    /// <returns>A <see cref="RegisterResult"/> indicating the outcome of the registration.</returns>
    Task<RegisterResult> RegisterUserAsync(string email, string password, string confirmEmailBaseUri, string? returnUrl);
}
