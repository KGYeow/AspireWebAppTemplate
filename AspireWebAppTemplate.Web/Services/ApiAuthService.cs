using System.Net.Http.Json;
using AspireWebAppTemplate.Core.Contracts;

namespace AspireWebAppTemplate.Web.Services;

/// <summary>
/// HTTP client service for authentication operations (login, logout, register, password change).
/// Calls the API's AuthController endpoints.
/// </summary>
public class ApiAuthService(HttpClient http)
{
    #region Authentication (Login, Register, Logout)

    /// <summary>
    /// Validates a single-use login token with the API and returns user claims for cookie creation.
    /// Called by the PerformLogin minimal API endpoint.
    /// </summary>
    public async Task<LoginTokenValidationResult?> ValidateLoginTokenAsync(string token)
    {
        var response = await http.PostAsJsonAsync("/api/auth/validate-token", new ValidateTokenRequest { Token = token });
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<LoginTokenValidationResult>();
        return null;
    }

    /// <summary>
    /// Authenticates a user with email and password, returning a login token.
    /// </summary>
    public async Task<LoginResult?> LoginAsync(LoginRequest request)
        => await http.PostAsJsonAsync("/api/auth/login", request)
            .ContinueWith(t => t.Result.Content.ReadFromJsonAsync<LoginResult>()).Unwrap();

    /// <summary>
    /// Registers a new user account with email and password.
    /// </summary>
    public async Task<RegisterResult?> RegisterAsync(LoginRequest request)
        => await http.PostAsJsonAsync("/api/auth/register", request)
            .ContinueWith(t => t.Result.Content.ReadFromJsonAsync<RegisterResult>()).Unwrap();

    /// <summary>
    /// Signs the current user out of the system.
    /// </summary>
    public async Task LogoutAsync()
        => await http.PostAsync("/api/auth/logout", null);

    #endregion

    #region User Profile + Password

    /// <summary>
    /// Returns the currently authenticated user's profile information.
    /// </summary>
    public async Task<UserDto?> GetCurrentUserAsync()
        => await http.GetFromJsonAsync<UserDto>("/api/auth/me");

    /// <summary>
    /// Updates the current user's display name and profile details.
    /// </summary>
    public async Task<string?> UpdateProfileAsync(UpdateProfileRequest request)
    {
        var response = await http.PutAsJsonAsync("/api/auth/profile", request);
        if (response.IsSuccessStatusCode) return null;
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Updates the current user's UI preferences (theme, timezone, date format).
    /// </summary>
    public async Task<string?> UpdatePreferencesAsync(UpdatePreferencesRequest request)
    {
        var response = await http.PutAsJsonAsync("/api/auth/preferences", request);
        if (response.IsSuccessStatusCode) return null;
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Changes the current user's password given the old and new passwords.
    /// </summary>
    public async Task<string?> ChangePasswordAsync(ChangePasswordRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/auth/change-password", request);
        if (response.IsSuccessStatusCode) return null;
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Sets a password for a user who does not yet have one (e.g., external login users).
    /// </summary>
    public async Task<string?> SetPasswordAsync(SetPasswordRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/auth/set-password", request);
        if (response.IsSuccessStatusCode) return null;
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Initiates a password reset flow by sending a reset token to the specified email.
    /// </summary>
    public async Task<(bool Success, string? Error)> ForgotPasswordAsync(string email)
    {
        var response = await http.PostAsJsonAsync("/api/auth/forgot-password", new { Email = email });
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Resets the user's password using a previously generated reset code.
    /// </summary>
    public async Task<(bool Success, string? Error)> ResetPasswordAsync(string email, string code, string newPassword)
    {
        var response = await http.PostAsJsonAsync("/api/auth/reset-password", new { Email = email, Code = code, NewPassword = newPassword });
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Confirms a user's email address using a confirmation code.
    /// </summary>
    public async Task<(bool Success, string? Error)> ConfirmEmailAsync(string userId, string code)
    {
        var response = await http.PostAsJsonAsync("/api/auth/confirm-email", new { UserId = userId, Code = code });
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    #endregion

    #region Email Management + Personal Data

    /// <summary>
    /// Returns the current user's email address and confirmation status.
    /// </summary>
    public async Task<EmailInfoDto?> GetEmailInfoAsync()
        => await http.GetFromJsonAsync<EmailInfoDto>("/api/auth/email");

    /// <summary>
    /// Initiates an email change for the current user.
    /// </summary>
    public async Task<(bool Success, string? Error)> ChangeEmailAsync(string newEmail)
    {
        var response = await http.PostAsJsonAsync("/api/auth/change-email", new ChangeEmailRequest { NewEmail = newEmail });
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Resends the email verification/confirmation email.
    /// </summary>
    public async Task<(bool Success, string? Error)> SendVerificationEmailAsync()
    {
        var response = await http.PostAsync("/api/auth/send-verification-email", null);
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Downloads the current user's personal data as a JSON file.
    /// </summary>
    public async Task<byte[]?> DownloadPersonalDataAsync()
    {
        var response = await http.PostAsync("/api/auth/download-personal-data", null);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadAsByteArrayAsync();
        return null;
    }

    /// <summary>
    /// Permanently deletes the current user's account after password confirmation.
    /// </summary>
    public async Task<(bool Success, string? Error)> DeleteAccountAsync(string password)
    {
        var response = await http.PostAsJsonAsync("/api/auth/delete-account", new DeleteAccountRequest { Password = password });
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    #endregion

    #region Two-Factor Authentication

    /// <summary>
    /// Returns the current user's two-factor authentication status.
    /// </summary>
    public async Task<TwoFactorStatusDto?> Get2faStatusAsync()
        => await http.GetFromJsonAsync<TwoFactorStatusDto>("/api/auth/2fa-status");

    /// <summary>
    /// Returns the shared key and authenticator URI for TOTP setup.
    /// </summary>
    public async Task<AuthenticatorSetupDto?> GetAuthenticatorSetupAsync()
        => await http.GetFromJsonAsync<AuthenticatorSetupDto>("/api/auth/authenticator-setup");

    /// <summary>
    /// Verifies a TOTP code and enables two-factor authentication for the user.
    /// </summary>
    public async Task<VerifyAuthenticatorResult?> VerifyAuthenticatorAsync(string code)
    {
        var response = await http.PostAsJsonAsync("/api/auth/verify-authenticator", new VerifyAuthenticatorRequest { Code = code });
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<VerifyAuthenticatorResult>();
        return null;
    }

    /// <summary>
    /// Disables two-factor authentication for the current user.
    /// </summary>
    public async Task<(bool Success, string? Error)> Disable2faAsync()
    {
        var response = await http.PostAsync("/api/auth/disable-2fa", null);
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Generates a new set of recovery codes for the current user.
    /// </summary>
    public async Task<string[]?> GenerateRecoveryCodesAsync()
    {
        var response = await http.PostAsync("/api/auth/generate-recovery-codes", null);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<string[]>();
        return null;
    }

    /// <summary>
    /// Resets the authenticator key, disabling the current authenticator app.
    /// </summary>
    public async Task<(bool Success, string? Error)> ResetAuthenticatorAsync()
    {
        var response = await http.PostAsync("/api/auth/reset-authenticator", null);
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Authenticates a user with a two-factor TOTP code.
    /// </summary>
    public async Task<LoginResult?> LoginWith2faAsync(LoginWith2faRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/auth/login-2fa", request);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<LoginResult>();
        return null;
    }

    /// <summary>
    /// Authenticates a user with a recovery code when the authenticator is unavailable.
    /// </summary>
    public async Task<LoginResult?> LoginWithRecoveryCodeAsync(string recoveryCode)
    {
        var response = await http.PostAsJsonAsync("/api/auth/login-recovery-code", new LoginWithRecoveryCodeRequest { RecoveryCode = recoveryCode });
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<LoginResult>();
        return null;
    }

    #endregion

    #region Passkeys + External Logins

    /// <summary>
    /// Returns all passkeys registered for the current user.
    /// </summary>
    public async Task<List<PasskeyInfoDto>?> GetPasskeysAsync()
        => await http.GetFromJsonAsync<List<PasskeyInfoDto>>("/api/auth/passkeys");

    /// <summary>
    /// Deletes a passkey by its credential identifier.
    /// </summary>
    public async Task<(bool Success, string? Error)> DeletePasskeyAsync(string credentialId)
    {
        var response = await http.DeleteAsync($"/api/auth/passkeys/{credentialId}");
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Renames a passkey to a user-friendly display name.
    /// </summary>
    public async Task<(bool Success, string? Error)> RenamePasskeyAsync(string credentialId, string name)
    {
        var response = await http.PutAsJsonAsync($"/api/auth/passkeys/{credentialId}/rename", new RenamePasskeyRequest { Name = name });
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Returns the current user's linked external login providers.
    /// </summary>
    public async Task<ExternalLoginsDto?> GetExternalLoginsAsync()
        => await http.GetFromJsonAsync<ExternalLoginsDto>("/api/auth/external-logins");

    /// <summary>
    /// Removes a linked external login provider from the current user's account.
    /// </summary>
    public async Task<(bool Success, string? Error)> RemoveExternalLoginAsync(string loginProvider, string providerKey)
    {
        var response = await http.PostAsJsonAsync("/api/auth/remove-external-login",
            new RemoveExternalLoginRequest { LoginProvider = loginProvider, ProviderKey = providerKey });
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    #endregion
}
