using System.Net.Http.Json;
using AspireWebAppTemplate.Core.Contracts;

namespace AspireWebAppTemplate.Web.Services;

/// <summary>
/// HTTP client service for authentication operations (login, logout, register, password change).
/// Calls the API's AuthController endpoints.
/// </summary>
public class ApiAuthService(HttpClient http)
{
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

    public async Task<LoginResult?> LoginAsync(LoginRequest request)
        => await http.PostAsJsonAsync("/api/auth/login", request)
            .ContinueWith(t => t.Result.Content.ReadFromJsonAsync<LoginResult>()).Unwrap();

    public async Task<RegisterResult?> RegisterAsync(LoginRequest request)
        => await http.PostAsJsonAsync("/api/auth/register", request)
            .ContinueWith(t => t.Result.Content.ReadFromJsonAsync<RegisterResult>()).Unwrap();

    public async Task LogoutAsync()
        => await http.PostAsync("/api/auth/logout", null);

    public async Task<string?> ChangePasswordAsync(ChangePasswordRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/auth/change-password", request);
        if (response.IsSuccessStatusCode) return null;
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<UserDto?> GetCurrentUserAsync()
        => await http.GetFromJsonAsync<UserDto>("/api/auth/me");

    public async Task<string?> UpdatePreferencesAsync(UpdatePreferencesRequest request)
    {
        var response = await http.PutAsJsonAsync("/api/auth/preferences", request);
        if (response.IsSuccessStatusCode) return null;
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string?> SetPasswordAsync(SetPasswordRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/auth/set-password", request);
        if (response.IsSuccessStatusCode) return null;
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string?> UpdateProfileAsync(UpdateProfileRequest request)
    {
        var response = await http.PutAsJsonAsync("/api/auth/profile", request);
        if (response.IsSuccessStatusCode) return null;
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<(bool Success, string? Error)> ForgotPasswordAsync(string email)
    {
        var response = await http.PostAsJsonAsync("/api/auth/forgot-password", new { Email = email });
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<(bool Success, string? Error)> ResetPasswordAsync(string email, string code, string newPassword)
    {
        var response = await http.PostAsJsonAsync("/api/auth/reset-password", new { Email = email, Code = code, NewPassword = newPassword });
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<(bool Success, string? Error)> ConfirmEmailAsync(string userId, string code)
    {
        var response = await http.PostAsJsonAsync("/api/auth/confirm-email", new { UserId = userId, Code = code });
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    // ─── Phase 2: Email Management + Personal Data ────────────────────────

    public async Task<EmailInfoDto?> GetEmailInfoAsync()
        => await http.GetFromJsonAsync<EmailInfoDto>("/api/auth/email");

    public async Task<(bool Success, string? Error)> ChangeEmailAsync(string newEmail)
    {
        var response = await http.PostAsJsonAsync("/api/auth/change-email", new ChangeEmailRequest { NewEmail = newEmail });
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<(bool Success, string? Error)> SendVerificationEmailAsync()
    {
        var response = await http.PostAsync("/api/auth/send-verification-email", null);
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<byte[]?> DownloadPersonalDataAsync()
    {
        var response = await http.PostAsync("/api/auth/download-personal-data", null);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadAsByteArrayAsync();
        return null;
    }

    public async Task<(bool Success, string? Error)> DeleteAccountAsync(string password)
    {
        var response = await http.PostAsJsonAsync("/api/auth/delete-account", new DeleteAccountRequest { Password = password });
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    // ─── Phase 3: Two-Factor Authentication ───────────────────────────────

    public async Task<TwoFactorStatusDto?> Get2faStatusAsync()
        => await http.GetFromJsonAsync<TwoFactorStatusDto>("/api/auth/2fa-status");

    public async Task<AuthenticatorSetupDto?> GetAuthenticatorSetupAsync()
        => await http.GetFromJsonAsync<AuthenticatorSetupDto>("/api/auth/authenticator-setup");

    public async Task<VerifyAuthenticatorResult?> VerifyAuthenticatorAsync(string code)
    {
        var response = await http.PostAsJsonAsync("/api/auth/verify-authenticator", new VerifyAuthenticatorRequest { Code = code });
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<VerifyAuthenticatorResult>();
        return null;
    }

    public async Task<(bool Success, string? Error)> Disable2faAsync()
    {
        var response = await http.PostAsync("/api/auth/disable-2fa", null);
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<string[]?> GenerateRecoveryCodesAsync()
    {
        var response = await http.PostAsync("/api/auth/generate-recovery-codes", null);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<string[]>();
        return null;
    }

    public async Task<(bool Success, string? Error)> ResetAuthenticatorAsync()
    {
        var response = await http.PostAsync("/api/auth/reset-authenticator", null);
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<LoginResult?> LoginWith2faAsync(LoginWith2faRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/auth/login-2fa", request);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<LoginResult>();
        return null;
    }

    public async Task<LoginResult?> LoginWithRecoveryCodeAsync(string recoveryCode)
    {
        var response = await http.PostAsJsonAsync("/api/auth/login-recovery-code", new LoginWithRecoveryCodeRequest { RecoveryCode = recoveryCode });
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<LoginResult>();
        return null;
    }

    // ─── Phase 4: Passkeys + External Logins ──────────────────────────────

    public async Task<List<PasskeyInfoDto>?> GetPasskeysAsync()
        => await http.GetFromJsonAsync<List<PasskeyInfoDto>>("/api/auth/passkeys");

    public async Task<(bool Success, string? Error)> DeletePasskeyAsync(string credentialId)
    {
        var response = await http.DeleteAsync($"/api/auth/passkeys/{credentialId}");
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<(bool Success, string? Error)> RenamePasskeyAsync(string credentialId, string name)
    {
        var response = await http.PutAsJsonAsync($"/api/auth/passkeys/{credentialId}/rename", new RenamePasskeyRequest { Name = name });
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<ExternalLoginsDto?> GetExternalLoginsAsync()
        => await http.GetFromJsonAsync<ExternalLoginsDto>("/api/auth/external-logins");

    public async Task<(bool Success, string? Error)> RemoveExternalLoginAsync(string loginProvider, string providerKey)
    {
        var response = await http.PostAsJsonAsync("/api/auth/remove-external-login",
            new RemoveExternalLoginRequest { LoginProvider = loginProvider, ProviderKey = providerKey });
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }
}
