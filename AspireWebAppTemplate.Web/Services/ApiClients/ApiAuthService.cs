using System.Net.Http.Json;
using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Application.Features.Authentication;
using AspireWebAppTemplate.Application.Features.AuditLog;
using AspireWebAppTemplate.Application.Features.Roles;
using AspireWebAppTemplate.Application.Features.Users;
using AspireWebAppTemplate.Web.Extensions;

namespace AspireWebAppTemplate.Web.Services;

/// <summary>
/// HTTP client service for authentication operations (login, logout, register, password change).
/// Calls the API's AuthController endpoints.
/// </summary>
public class ApiAuthService
{
    #region Constructor

    /// <summary>
    /// The underlying HttpClient configured with the ApiService base address.
    /// </summary>
    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of <see cref="ApiAuthService"/> with the configured HttpClient.
    /// </summary>
    /// <param name="http">The HttpClient instance configured via Aspire service discovery.</param>
    public ApiAuthService(HttpClient http)
    {
        _http = http;
    }

    #endregion

    #region Authentication (Login, Register, Logout)

    /// <summary>
    /// Validates a single-use login token with the API and returns user claims for cookie creation.
    /// Called by the PerformLogin minimal API endpoint.
    /// </summary>
    public async Task<ApiResult<LoginTokenValidationResult>> ValidateLoginTokenAsync(string token)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/validate-token", new ValidateTokenRequest { Token = token });
        return await response.ToApiResultAsync<LoginTokenValidationResult>();
    }

    /// <summary>
    /// Authenticates a user with email and password, returning a login token.
    /// </summary>
    public async Task<ApiResult<LoginResult>> LoginAsync(LoginRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/login", request);
        return await response.ToApiResultAsync<LoginResult>();
    }

    /// <summary>
    /// Registers a new user account with email and password.
    /// </summary>
    public async Task<ApiResult<RegisterResult>> RegisterAsync(LoginRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/register", request);
        return await response.ToApiResultAsync<RegisterResult>();
    }

    /// <summary>
    /// Signs the current user out of the system.
    /// </summary>
    public async Task LogoutAsync()
        => await _http.PostAsync("/api/auth/logout", null);

    #endregion

    #region User Profile + Password

    /// <summary>
    /// Returns the currently authenticated user's profile information.
    /// </summary>
    public async Task<ApiResult<UserDto>> GetCurrentUserAsync()
    {
        var response = await _http.GetAsync("/api/auth/me");
        return await response.ToApiResultAsync<UserDto>();
    }

    /// <summary>
    /// Updates the current user's display name and profile details.
    /// </summary>
    public async Task<ApiResult> UpdateProfileAsync(UpdateProfileRequest request)
    {
        var response = await _http.PutAsJsonAsync("/api/auth/profile", request);
        return await response.ToApiResultAsync();
    }

    /// <summary>
    /// Updates the current user's UI preferences (theme, timezone, date format).
    /// </summary>
    public async Task<ApiResult> UpdatePreferencesAsync(UpdatePreferencesRequest request)
    {
        var response = await _http.PutAsJsonAsync("/api/auth/preferences", request);
        return await response.ToApiResultAsync();
    }

    /// <summary>
    /// Changes the current user's password given the old and new passwords.
    /// </summary>
    public async Task<ApiResult> ChangePasswordAsync(ChangePasswordRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/change-password", request);
        return await response.ToApiResultAsync();
    }

    /// <summary>
    /// Sets a password for a user who does not yet have one (e.g., external login users).
    /// </summary>
    public async Task<ApiResult> SetPasswordAsync(SetPasswordRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/set-password", request);
        return await response.ToApiResultAsync();
    }

    /// <summary>
    /// Initiates a password reset flow by sending a reset token to the specified email.
    /// </summary>
    public async Task<ApiResult> ForgotPasswordAsync(string email)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/forgot-password", new { Email = email });
        return await response.ToApiResultAsync();
    }

    /// <summary>
    /// Resets the user's password using a previously generated reset code.
    /// </summary>
    public async Task<ApiResult> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/reset-password", request);
        return await response.ToApiResultAsync();
    }

    /// <summary>
    /// Confirms a user's email address using a confirmation code.
    /// </summary>
    public async Task<ApiResult> ConfirmEmailAsync(ConfirmEmailRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/confirm-email", request);
        return await response.ToApiResultAsync();
    }

    #endregion

    #region Email Management + Personal Data

    /// <summary>
    /// Returns the current user's email address and confirmation status.
    /// </summary>
    public async Task<ApiResult<EmailInfoDto>> GetEmailInfoAsync()
    {
        var response = await _http.GetAsync("/api/auth/email");
        return await response.ToApiResultAsync<EmailInfoDto>();
    }

    /// <summary>
    /// Initiates an email change for the current user.
    /// </summary>
    public async Task<ApiResult> ChangeEmailAsync(string newEmail)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/change-email", new ChangeEmailRequest { NewEmail = newEmail });
        return await response.ToApiResultAsync();
    }

    /// <summary>
    /// Resends the email verification/confirmation email.
    /// </summary>
    public async Task<ApiResult> SendVerificationEmailAsync()
    {
        var response = await _http.PostAsync("/api/auth/send-verification-email", null);
        return await response.ToApiResultAsync();
    }

    /// <summary>
    /// Downloads the current user's personal data as a JSON file.
    /// </summary>
    public async Task<ApiResult<byte[]>> DownloadPersonalDataAsync()
    {
        var response = await _http.PostAsync("/api/auth/download-personal-data", null);
        if (response.IsSuccessStatusCode)
            return ApiResult<byte[]>.Success(await response.Content.ReadAsByteArrayAsync());
        return ApiResult<byte[]>.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Permanently deletes the current user's account after password confirmation.
    /// </summary>
    public async Task<ApiResult> DeleteAccountAsync(string password)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/delete-account", new DeleteAccountRequest { Password = password });
        return await response.ToApiResultAsync();
    }

    #endregion

    #region Two-Factor Authentication

    /// <summary>
    /// Returns the current user's two-factor authentication status.
    /// </summary>
    public async Task<ApiResult<TwoFactorStatusDto>> Get2faStatusAsync()
    {
        var response = await _http.GetAsync("/api/auth/2fa-status");
        return await response.ToApiResultAsync<TwoFactorStatusDto>();
    }

    /// <summary>
    /// Returns the shared key and authenticator URI for TOTP setup.
    /// </summary>
    public async Task<ApiResult<AuthenticatorSetupDto>> GetAuthenticatorSetupAsync()
    {
        var response = await _http.GetAsync("/api/auth/authenticator-setup");
        return await response.ToApiResultAsync<AuthenticatorSetupDto>();
    }

    /// <summary>
    /// Verifies a TOTP code and enables two-factor authentication for the user.
    /// </summary>
    public async Task<ApiResult<VerifyAuthenticatorResult>> VerifyAuthenticatorAsync(string code)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/verify-authenticator", new VerifyAuthenticatorRequest { Code = code });
        return await response.ToApiResultAsync<VerifyAuthenticatorResult>();
    }

    /// <summary>
    /// Disables two-factor authentication for the current user.
    /// </summary>
    public async Task<ApiResult> Disable2faAsync()
    {
        var response = await _http.PostAsync("/api/auth/disable-2fa", null);
        return await response.ToApiResultAsync();
    }

    /// <summary>
    /// Generates a new set of recovery codes for the current user.
    /// </summary>
    public async Task<ApiResult<string[]>> GenerateRecoveryCodesAsync()
    {
        var response = await _http.PostAsync("/api/auth/generate-recovery-codes", null);
        return await response.ToApiResultAsync<string[]>();
    }

    /// <summary>
    /// Resets the authenticator key, disabling the current authenticator app.
    /// </summary>
    public async Task<ApiResult> ResetAuthenticatorAsync()
    {
        var response = await _http.PostAsync("/api/auth/reset-authenticator", null);
        return await response.ToApiResultAsync();
    }

    /// <summary>
    /// Authenticates a user with a two-factor TOTP code.
    /// </summary>
    public async Task<ApiResult<LoginResult>> LoginWith2faAsync(LoginWith2faRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/login-2fa", request);
        return await response.ToApiResultAsync<LoginResult>();
    }

    /// <summary>
    /// Authenticates a user with a recovery code when the authenticator is unavailable.
    /// </summary>
    public async Task<ApiResult<LoginResult>> LoginWithRecoveryCodeAsync(string recoveryCode)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/login-recovery-code", new LoginWithRecoveryCodeRequest { RecoveryCode = recoveryCode });
        return await response.ToApiResultAsync<LoginResult>();
    }

    #endregion

    #region Passkeys + External Logins

    /// <summary>
    /// Returns all passkeys registered for the current user.
    /// </summary>
    public async Task<ApiResult<List<PasskeyInfoDto>>> GetPasskeysAsync()
    {
        var response = await _http.GetAsync("/api/auth/passkeys");
        return await response.ToApiResultAsync<List<PasskeyInfoDto>>();
    }

    /// <summary>
    /// Deletes a passkey by its credential identifier.
    /// </summary>
    public async Task<ApiResult> DeletePasskeyAsync(string credentialId)
    {
        var response = await _http.DeleteAsync($"/api/auth/passkeys/{credentialId}");
        return await response.ToApiResultAsync();
    }

    /// <summary>
    /// Renames a passkey to a user-friendly display name.
    /// </summary>
    public async Task<ApiResult> RenamePasskeyAsync(string credentialId, string name)
    {
        var response = await _http.PutAsJsonAsync($"/api/auth/passkeys/{credentialId}/rename", new RenamePasskeyRequest { Name = name });
        return await response.ToApiResultAsync();
    }

    /// <summary>
    /// Returns the current user's linked external login providers.
    /// </summary>
    public async Task<ApiResult<ExternalLoginsDto>> GetExternalLoginsAsync()
    {
        var response = await _http.GetAsync("/api/auth/external-logins");
        return await response.ToApiResultAsync<ExternalLoginsDto>();
    }

    /// <summary>
    /// Removes a linked external login provider from the current user's account.
    /// </summary>
    public async Task<ApiResult> RemoveExternalLoginAsync(string loginProvider, string providerKey)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/remove-external-login",
            new RemoveExternalLoginRequest { LoginProvider = loginProvider, ProviderKey = providerKey });
        return await response.ToApiResultAsync();
    }

    #endregion
}
