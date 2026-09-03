using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Infrastructure.Utilities;
using AspireWebAppTemplate.Application.Features.Template.AuditLog;
using AspireWebAppTemplate.Application.Features.Template.Authentication;
using AspireWebAppTemplate.Application.Features.Template.Users;
using AspireWebAppTemplate.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace AspireWebAppTemplate.Infrastructure.Services.Template.Authentication;

/// <summary>
/// Implements <see cref="IAuthService"/> with full account self-management operations
/// including profile, preferences, password, email, two-factor authentication, personal data,
/// external logins, and passkeys. All methods operate on the currently authenticated user
/// resolved via <see cref="ICurrentUserAccessor"/>.
/// </summary>
/// <remarks>
/// Registered as a scoped service to align with the per-request <see cref="UserManager{TUser}"/>
/// and <see cref="SignInManager{TUser}"/> lifetimes. Controllers delegate to this service
/// without touching UserManager, SignInManager, or ApplicationDbContext directly.
/// </remarks>
public class AuthService : IAuthService
{
    #region Constructor

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserAccessor _currentUser;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthService"/> class.
    /// </summary>
    /// <param name="userManager">The ASP.NET Core Identity user manager for user operations.</param>
    /// <param name="signInManager">The ASP.NET Core Identity sign-in manager for authentication operations.</param>
    /// <param name="auditLogService">The audit log service for recording security-sensitive actions.</param>
    /// <param name="currentUser">The current user accessor for identity and IP address resolution.</param>
    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IAuditLogService auditLogService,
        ICurrentUserAccessor currentUser)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _auditLogService = auditLogService;
        _currentUser = currentUser;
    }

    #endregion

    #region Profile

    /// <inheritdoc />
    public async Task<UserDto> GetProfileAsync()
    {
        var user = await GetCurrentUserAsync();
        var roles = await _userManager.GetRolesAsync(user);

        return MapToUserDto(user, roles);
    }

    /// <inheritdoc />
    public async Task UpdateProfileAsync(UpdateProfileRequest request)
    {
        var user = await GetCurrentUserAsync();

        if (request.DisplayName is not null) user.DisplayName = request.DisplayName;
        if (request.FirstName is not null) user.FirstName = request.FirstName;
        if (request.LastName is not null) user.LastName = request.LastName;

        if (request.PhoneNumber is not null)
        {
            var setPhoneResult = await _userManager.SetPhoneNumberAsync(user, request.PhoneNumber);
            if (!setPhoneResult.Succeeded)
                throw new InvalidOperationException(string.Join("; ", setPhoneResult.Errors.Select(e => e.Description)));
        }

        user.UpdatedUtc = DateTime.UtcNow;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    /// <inheritdoc />
    public async Task UpdatePreferencesAsync(UpdatePreferencesRequest request)
    {
        var user = await GetCurrentUserAsync();

        if (request.Theme.HasValue)
            user.Theme = request.Theme.Value;
        if (request.TimeZoneId is not null)
            user.TimeZoneId = string.IsNullOrEmpty(request.TimeZoneId) ? null : request.TimeZoneId;
        if (request.DateTimeFormat is not null)
            user.DateTimeFormat = string.IsNullOrEmpty(request.DateTimeFormat) ? null : request.DateTimeFormat;
        if (request.NotificationPopupsEnabled.HasValue)
            user.NotificationPopupsEnabled = request.NotificationPopupsEnabled.Value;

        user.UpdatedUtc = DateTime.UtcNow;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    #endregion

    #region Password

    /// <inheritdoc />
    public async Task ChangePasswordAsync(ChangePasswordRequest request)
    {
        var user = await GetCurrentUserAsync();

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException(errors);
        }

        user.LastPasswordChangeUtc = DateTime.UtcNow;
        user.UpdatedUtc = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        await _auditLogService.LogAsync(new AuditLogRequest
        {
            UserId = _currentUser.UserId,
            ActionType = AuditActionType.PasswordChanged,
            EntityType = AuditEntityType.User,
            EntityId = _currentUser.UserId ?? "",
            EntityName = user.DisplayName ?? user.UserName ?? "Unknown",
            Description = $"User '{user.DisplayName ?? user.UserName}' changed their password.",
            NewValues = AuditChangeHelper.Serialize(new { PasswordChanged = true }),
            IpAddress = _currentUser.IpAddress
        });
    }

    /// <inheritdoc />
    public async Task SetPasswordAsync(SetPasswordRequest request)
    {
        var user = await GetCurrentUserAsync();

        if (await _userManager.HasPasswordAsync(user))
            throw new InvalidOperationException("User already has a password. Use change-password instead.");

        var result = await _userManager.AddPasswordAsync(user, request.NewPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    #endregion

    #region Email

    /// <inheritdoc />
    public async Task<EmailInfoDto> GetEmailAsync()
    {
        var user = await GetCurrentUserAsync();

        return new EmailInfoDto
        {
            Email = user.Email ?? "",
            IsEmailConfirmed = user.EmailConfirmed
        };
    }

    /// <inheritdoc />
    public async Task ChangeEmailAsync(ChangeEmailRequest request)
    {
        var user = await GetCurrentUserAsync();

        if (string.Equals(user.Email, request.NewEmail, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The new email is the same as the current email.");

        var code = await _userManager.GenerateChangeEmailTokenAsync(user, request.NewEmail);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        // Apply the change directly (in production, confirm via email link)
        var decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        var result = await _userManager.ChangeEmailAsync(user, request.NewEmail, decodedCode);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        // Also update username to match email
        await _userManager.SetUserNameAsync(user, request.NewEmail);

        await _auditLogService.LogAsync(new AuditLogRequest
        {
            UserId = _currentUser.UserId,
            ActionType = AuditActionType.ProfileUpdated,
            EntityType = AuditEntityType.User,
            EntityId = _currentUser.UserId ?? "",
            EntityName = user.DisplayName ?? user.UserName ?? "",
            Description = $"User '{user.DisplayName ?? user.UserName}' changed their email address.",
            OldValues = AuditChangeHelper.Serialize(new { Email = user.Email }),
            NewValues = AuditChangeHelper.Serialize(new { Email = request.NewEmail }),
            IpAddress = _currentUser.IpAddress
        });
    }

    /// <inheritdoc />
    public async Task SendVerificationEmailAsync()
    {
        var user = await GetCurrentUserAsync();

        var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        // TODO: Send the verification email with the code
        // For now, this method generates the token but does not send an actual email
    }

    #endregion

    #region Two-Factor Authentication

    /// <inheritdoc />
    public async Task<TwoFactorStatusDto> Get2faStatusAsync()
    {
        var user = await GetCurrentUserAsync();

        return new TwoFactorStatusDto
        {
            HasAuthenticator = await _userManager.GetAuthenticatorKeyAsync(user) is not null,
            Is2faEnabled = await _userManager.GetTwoFactorEnabledAsync(user),
            IsMachineRemembered = await _signInManager.IsTwoFactorClientRememberedAsync(user),
            RecoveryCodesLeft = await _userManager.CountRecoveryCodesAsync(user)
        };
    }

    /// <inheritdoc />
    public async Task<AuthenticatorSetupDto> GetAuthenticatorSetupAsync()
    {
        var user = await GetCurrentUserAsync();

        var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(unformattedKey))
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        var sharedKey = FormatKey(unformattedKey!);
        var email = await _userManager.GetEmailAsync(user);
        var authenticatorUri = GenerateQrCodeUri(email!, unformattedKey!);

        return new AuthenticatorSetupDto
        {
            SharedKey = sharedKey,
            AuthenticatorUri = authenticatorUri
        };
    }

    /// <inheritdoc />
    public async Task<VerifyAuthenticatorResult> VerifyAuthenticatorAsync(VerifyAuthenticatorRequest request)
    {
        var user = await GetCurrentUserAsync();

        var verificationCode = request.Code.Replace(" ", string.Empty).Replace("-", string.Empty);

        var is2faTokenValid = await _userManager.VerifyTwoFactorTokenAsync(
            user, _userManager.Options.Tokens.AuthenticatorTokenProvider, verificationCode);

        if (!is2faTokenValid)
        {
            return new VerifyAuthenticatorResult { Succeeded = false };
        }

        await _userManager.SetTwoFactorEnabledAsync(user, true);
        var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);

        await _auditLogService.LogAsync(new AuditLogRequest
        {
            UserId = _currentUser.UserId,
            ActionType = AuditActionType.SettingsChanged,
            EntityType = AuditEntityType.User,
            EntityId = _currentUser.UserId ?? "",
            EntityName = user.DisplayName ?? user.UserName ?? "",
            Description = $"User '{user.DisplayName ?? user.UserName}' enabled two-factor authentication.",
            NewValues = AuditChangeHelper.Serialize(new { TwoFactorEnabled = true }),
            IpAddress = _currentUser.IpAddress
        });

        return new VerifyAuthenticatorResult
        {
            Succeeded = true,
            RecoveryCodes = recoveryCodes?.ToArray()
        };
    }

    /// <inheritdoc />
    public async Task Disable2faAsync()
    {
        var user = await GetCurrentUserAsync();

        if (!await _userManager.GetTwoFactorEnabledAsync(user))
            throw new InvalidOperationException("Cannot disable 2FA as it is not currently enabled.");

        var result = await _userManager.SetTwoFactorEnabledAsync(user, false);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        await _auditLogService.LogAsync(new AuditLogRequest
        {
            UserId = _currentUser.UserId,
            ActionType = AuditActionType.SettingsChanged,
            EntityType = AuditEntityType.User,
            EntityId = _currentUser.UserId ?? "",
            EntityName = user.DisplayName ?? user.UserName ?? "",
            Description = $"User '{user.DisplayName ?? user.UserName}' disabled two-factor authentication.",
            NewValues = AuditChangeHelper.Serialize(new { TwoFactorEnabled = false }),
            IpAddress = _currentUser.IpAddress
        });
    }

    /// <inheritdoc />
    public async Task<string[]> GenerateRecoveryCodesAsync()
    {
        var user = await GetCurrentUserAsync();

        if (!await _userManager.GetTwoFactorEnabledAsync(user))
            throw new InvalidOperationException("Cannot generate recovery codes as 2FA is not enabled.");

        var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        return recoveryCodes?.ToArray() ?? [];
    }

    /// <inheritdoc />
    public async Task ResetAuthenticatorAsync()
    {
        var user = await GetCurrentUserAsync();

        await _userManager.SetTwoFactorEnabledAsync(user, false);
        await _userManager.ResetAuthenticatorKeyAsync(user);
        await _signInManager.RefreshSignInAsync(user);

        await _auditLogService.LogAsync(new AuditLogRequest
        {
            UserId = _currentUser.UserId,
            ActionType = AuditActionType.SettingsChanged,
            EntityType = AuditEntityType.User,
            EntityId = _currentUser.UserId ?? "",
            EntityName = user.DisplayName ?? user.UserName ?? "",
            Description = $"User '{user.DisplayName ?? user.UserName}' reset their authenticator app.",
            NewValues = AuditChangeHelper.Serialize(new { AuthenticatorReset = true, TwoFactorEnabled = false }),
            IpAddress = _currentUser.IpAddress
        });
    }

    #endregion

    #region Personal Data & Account

    /// <inheritdoc />
    public async Task<byte[]> DownloadPersonalDataAsync()
    {
        var user = await GetCurrentUserAsync();

        var personalData = new Dictionary<string, string>();

        // Include all properties marked with [PersonalData] attribute
        var personalDataProps = typeof(ApplicationUser).GetProperties()
            .Where(prop => Attribute.IsDefined(prop, typeof(PersonalDataAttribute)));

        foreach (var p in personalDataProps)
        {
            personalData.Add(p.Name, p.GetValue(user)?.ToString() ?? "null");
        }

        // Add Identity-specific data that should always be included
        personalData.Add("Id", user.Id);
        personalData.Add("UserName", user.UserName ?? "");
        personalData.Add("Email", user.Email ?? "");
        personalData.Add("EmailConfirmed", user.EmailConfirmed.ToString());
        personalData.Add("PhoneNumber", user.PhoneNumber ?? "");
        personalData.Add("PhoneNumberConfirmed", user.PhoneNumberConfirmed.ToString());
        personalData.Add("TwoFactorEnabled", user.TwoFactorEnabled.ToString());

        // Include external login provider keys
        var logins = await _userManager.GetLoginsAsync(user);
        foreach (var login in logins)
        {
            personalData.Add($"{login.LoginProvider} external login provider key", login.ProviderKey);
        }

        return JsonSerializer.SerializeToUtf8Bytes(personalData, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <inheritdoc />
    public async Task DeleteAccountAsync(DeleteAccountRequest request)
    {
        var user = await GetCurrentUserAsync();

        if (await _userManager.HasPasswordAsync(user))
        {
            if (string.IsNullOrEmpty(request.Password))
                throw new InvalidOperationException("Password is required.");

            if (!await _userManager.CheckPasswordAsync(user, request.Password))
                throw new InvalidOperationException("Incorrect password.");
        }

        var userName = user.DisplayName ?? user.UserName ?? "Unknown";
        var userId = _currentUser.UserId;

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        await _signInManager.SignOutAsync();

        await _auditLogService.LogAsync(new AuditLogRequest
        {
            UserId = userId,
            ActionType = AuditActionType.UserDeleted,
            EntityType = AuditEntityType.User,
            EntityId = userId ?? "",
            EntityName = userName,
            Description = $"User '{userName}' deleted their account.",
            IpAddress = _currentUser.IpAddress
        });
    }

    #endregion

    #region External Logins

    /// <inheritdoc />
    public async Task<ExternalLoginsDto> GetExternalLoginsAsync()
    {
        var user = await GetCurrentUserAsync();

        var currentLogins = await _userManager.GetLoginsAsync(user);
        var hasPassword = await _userManager.HasPasswordAsync(user);

        return new ExternalLoginsDto
        {
            CurrentLogins = currentLogins.Select(l => new ExternalLoginInfoDto
            {
                LoginProvider = l.LoginProvider,
                ProviderDisplayName = l.ProviderDisplayName ?? l.LoginProvider,
                ProviderKey = l.ProviderKey
            }).ToList(),
            ShowRemoveButton = hasPassword || currentLogins.Count > 1
        };
    }

    /// <inheritdoc />
    public async Task RemoveExternalLoginAsync(RemoveExternalLoginRequest request)
    {
        var user = await GetCurrentUserAsync();

        var result = await _userManager.RemoveLoginAsync(user, request.LoginProvider, request.ProviderKey);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        await _signInManager.RefreshSignInAsync(user);
    }

    #endregion

    #region Passkeys

    /// <inheritdoc />
    public async Task<List<PasskeyInfoDto>> GetPasskeysAsync()
    {
        // Ensure user exists
        await GetCurrentUserAsync();

        // Passkey support requires WebAuthn credential storage.
        // Return empty list as stub — full WebAuthn integration to be wired later.
        return new List<PasskeyInfoDto>();
    }

    /// <inheritdoc />
    public async Task DeletePasskeyAsync(string credentialId)
    {
        // Ensure user exists
        await GetCurrentUserAsync();

        // Stub: Passkey deletion requires WebAuthn credential storage.
        // Full implementation to be added when WebAuthn storage is available.
    }

    /// <inheritdoc />
    public async Task RenamePasskeyAsync(string credentialId, RenamePasskeyRequest request)
    {
        // Ensure user exists
        await GetCurrentUserAsync();

        // Stub: Passkey rename requires WebAuthn credential storage.
        // Full implementation to be added when WebAuthn storage is available.
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Retrieves the currently authenticated user from <see cref="ICurrentUserAccessor.UserId"/>.
    /// Throws <see cref="InvalidOperationException"/> if no authenticated user is available.
    /// </summary>
    private async Task<ApplicationUser> GetCurrentUserAsync()
    {
        var userId = _currentUser.UserId
            ?? throw new InvalidOperationException("No authenticated user.");

        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        return user;
    }

    /// <summary>
    /// Formats an unformatted authenticator key into groups of 4 characters separated by spaces.
    /// </summary>
    private static string FormatKey(string unformattedKey)
    {
        var result = new StringBuilder();
        var currentPosition = 0;
        while (currentPosition + 4 < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
            currentPosition += 4;
        }
        if (currentPosition < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition));
        }
        return result.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// Generates the otpauth:// URI used for QR code generation in authenticator app setup.
    /// </summary>
    private static string GenerateQrCodeUri(string email, string unformattedKey)
    {
        const string authenticatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";
        return string.Format(
            CultureInfo.InvariantCulture,
            authenticatorUriFormat,
            UrlEncoder.Default.Encode("AspireWebAppTemplate"),
            UrlEncoder.Default.Encode(email),
            unformattedKey);
    }

    /// <summary>
    /// Maps an <see cref="ApplicationUser"/> entity and its roles to a <see cref="UserDto"/>.
    /// </summary>
    private static UserDto MapToUserDto(ApplicationUser user, IList<string> roles)
    {
        return new UserDto
        {
            Id = user.Id,
            UserName = user.UserName ?? "",
            DisplayName = user.DisplayName,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            JobTitle = user.JobTitle,
            Department = user.Department,
            EmployeeNumber = user.EmployeeNumber,
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive,
            AuthSource = user.AuthSource.ToString(),
            Roles = roles.ToList(),
            CreatedUtc = user.CreatedUtc,
            UpdatedUtc = user.UpdatedUtc,
            Theme = user.Theme,
            TimeZoneId = user.TimeZoneId,
            DateTimeFormat = user.DateTimeFormat,
            NotificationPopupsEnabled = user.NotificationPopupsEnabled
        };
    }

    #endregion
}
