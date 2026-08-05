using AspireWebAppTemplate.Application.Contracts.Auth;
using AspireWebAppTemplate.Application.Contracts.Users;

namespace AspireWebAppTemplate.Application.Abstractions;

/// <summary>
/// Defines the contract for all account self-management operations — profile, preferences,
/// password, email, two-factor authentication, personal data, external logins, and passkeys.
/// All methods operate on the currently authenticated user (from <see cref="ICurrentUserAccessor"/>).
/// </summary>
/// <remarks>
/// Implementations should be registered as scoped services to align with the per-request
/// <c>DbContext</c> lifetime. Controllers delegate to this service without touching
/// <c>UserManager</c>, <c>SignInManager</c>, or <c>ApplicationDbContext</c> directly.
/// </remarks>
public interface IAuthService
{
    #region Profile

    /// <summary>
    /// Retrieves the current user's profile information including preferences, roles, and security status.
    /// </summary>
    /// <returns>
    /// A task that resolves to a <see cref="UserDto"/> containing the authenticated user's
    /// full profile data.
    /// </returns>
    Task<UserDto> GetProfileAsync();

    /// <summary>
    /// Updates the current user's profile fields (display name, first name, last name, phone number).
    /// </summary>
    /// <param name="request">
    /// An <see cref="UpdateProfileRequest"/> containing the fields to update.
    /// </param>
    /// <returns>A task representing the asynchronous update operation.</returns>
    Task UpdateProfileAsync(UpdateProfileRequest request);

    /// <summary>
    /// Updates the current user's display preferences (theme, timezone, date/time format).
    /// </summary>
    /// <param name="request">
    /// An <see cref="UpdatePreferencesRequest"/> containing the preference values to set.
    /// </param>
    /// <returns>A task representing the asynchronous update operation.</returns>
    Task UpdatePreferencesAsync(UpdatePreferencesRequest request);

    #endregion

    #region Password

    /// <summary>
    /// Changes the current user's password, requiring the current password for verification.
    /// </summary>
    /// <param name="request">
    /// A <see cref="ChangePasswordRequest"/> containing the current password and new password.
    /// </param>
    /// <returns>A task representing the asynchronous password change operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the current password is incorrect or the new password does not meet policy requirements.
    /// </exception>
    Task ChangePasswordAsync(ChangePasswordRequest request);

    /// <summary>
    /// Sets an initial local password on an account that does not yet have one
    /// (e.g., accounts created via external login or LDAP).
    /// </summary>
    /// <param name="request">
    /// A <see cref="SetPasswordRequest"/> containing the new password to set.
    /// </param>
    /// <returns>A task representing the asynchronous set-password operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the account already has a password set.
    /// </exception>
    Task SetPasswordAsync(SetPasswordRequest request);

    #endregion

    #region Email

    /// <summary>
    /// Retrieves the current user's email address and confirmation status.
    /// </summary>
    /// <returns>
    /// A task that resolves to an <see cref="EmailInfoDto"/> containing the email and
    /// whether it has been confirmed.
    /// </returns>
    Task<EmailInfoDto> GetEmailAsync();

    /// <summary>
    /// Initiates an email address change for the current user. Sends a confirmation email
    /// to the new address.
    /// </summary>
    /// <param name="request">
    /// A <see cref="ChangeEmailRequest"/> containing the new email address.
    /// </param>
    /// <returns>A task representing the asynchronous email change initiation.</returns>
    Task ChangeEmailAsync(ChangeEmailRequest request);

    /// <summary>
    /// Sends a verification email to the current user's unconfirmed email address.
    /// </summary>
    /// <returns>A task representing the asynchronous send operation.</returns>
    Task SendVerificationEmailAsync();

    #endregion

    #region Two-Factor Authentication

    /// <summary>
    /// Retrieves the current user's two-factor authentication status including whether
    /// an authenticator is configured, 2FA is enabled, and recovery codes remaining.
    /// </summary>
    /// <returns>
    /// A task that resolves to a <see cref="TwoFactorStatusDto"/> containing the 2FA state.
    /// </returns>
    Task<TwoFactorStatusDto> Get2faStatusAsync();

    /// <summary>
    /// Generates and returns the shared key and authenticator URI needed to set up
    /// a TOTP authenticator app.
    /// </summary>
    /// <returns>
    /// A task that resolves to an <see cref="AuthenticatorSetupDto"/> containing the
    /// shared key and QR code URI.
    /// </returns>
    Task<AuthenticatorSetupDto> GetAuthenticatorSetupAsync();

    /// <summary>
    /// Verifies a TOTP code from the user's authenticator app during 2FA setup.
    /// On success, enables two-factor authentication and generates recovery codes.
    /// </summary>
    /// <param name="request">
    /// A <see cref="VerifyAuthenticatorRequest"/> containing the six-digit TOTP code.
    /// </param>
    /// <returns>
    /// A task that resolves to a <see cref="VerifyAuthenticatorResult"/> indicating
    /// whether verification succeeded and containing recovery codes on success.
    /// </returns>
    Task<VerifyAuthenticatorResult> VerifyAuthenticatorAsync(VerifyAuthenticatorRequest request);

    /// <summary>
    /// Disables two-factor authentication for the current user.
    /// </summary>
    /// <returns>A task representing the asynchronous disable operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when two-factor authentication is not currently enabled.
    /// </exception>
    Task Disable2faAsync();

    /// <summary>
    /// Generates a new set of recovery codes for the current user, replacing any existing codes.
    /// </summary>
    /// <returns>
    /// A task that resolves to an array of new recovery code strings.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when two-factor authentication is not currently enabled.
    /// </exception>
    Task<string[]> GenerateRecoveryCodesAsync();

    /// <summary>
    /// Resets the authenticator key for the current user, requiring them to re-configure
    /// their authenticator app. Does not disable 2FA — the user must set up a new authenticator.
    /// </summary>
    /// <returns>A task representing the asynchronous reset operation.</returns>
    Task ResetAuthenticatorAsync();

    #endregion

    #region Personal Data & Account

    /// <summary>
    /// Downloads the current user's personal data as a JSON byte array containing all
    /// properties decorated with <c>[PersonalData]</c> on the user entity.
    /// </summary>
    /// <returns>
    /// A task that resolves to a UTF-8 encoded JSON byte array containing the user's personal data.
    /// </returns>
    Task<byte[]> DownloadPersonalDataAsync();

    /// <summary>
    /// Permanently deletes the current user's account after verifying their password.
    /// </summary>
    /// <param name="request">
    /// A <see cref="DeleteAccountRequest"/> containing the user's password for verification.
    /// </param>
    /// <returns>A task representing the asynchronous account deletion.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the provided password is incorrect.
    /// </exception>
    Task DeleteAccountAsync(DeleteAccountRequest request);

    #endregion

    #region External Logins

    /// <summary>
    /// Retrieves the current user's linked external login providers and whether they can be removed.
    /// </summary>
    /// <returns>
    /// A task that resolves to an <see cref="ExternalLoginsDto"/> containing the list of
    /// linked providers and removal eligibility.
    /// </returns>
    Task<ExternalLoginsDto> GetExternalLoginsAsync();

    /// <summary>
    /// Removes an external login provider from the current user's account.
    /// </summary>
    /// <param name="request">
    /// A <see cref="RemoveExternalLoginRequest"/> identifying the provider and key to remove.
    /// </param>
    /// <returns>A task representing the asynchronous removal operation.</returns>
    Task RemoveExternalLoginAsync(RemoveExternalLoginRequest request);

    #endregion

    #region Passkeys

    /// <summary>
    /// Retrieves all registered passkeys (WebAuthn credentials) for the current user.
    /// </summary>
    /// <returns>
    /// A task that resolves to a list of <see cref="PasskeyInfoDto"/> representing the
    /// user's registered passkeys.
    /// </returns>
    Task<List<PasskeyInfoDto>> GetPasskeysAsync();

    /// <summary>
    /// Deletes a registered passkey by its credential ID.
    /// </summary>
    /// <param name="credentialId">The Base64-encoded credential ID of the passkey to delete.</param>
    /// <returns>A task representing the asynchronous deletion operation.</returns>
    Task DeletePasskeyAsync(string credentialId);

    /// <summary>
    /// Renames a registered passkey's friendly name.
    /// </summary>
    /// <param name="credentialId">The Base64-encoded credential ID of the passkey to rename.</param>
    /// <param name="request">
    /// A <see cref="RenamePasskeyRequest"/> containing the new friendly name.
    /// </param>
    /// <returns>A task representing the asynchronous rename operation.</returns>
    Task RenamePasskeyAsync(string credentialId, RenamePasskeyRequest request);

    #endregion
}
