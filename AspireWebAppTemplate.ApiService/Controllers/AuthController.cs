using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Application.Contracts.Auth;
using AspireWebAppTemplate.Application.Contracts.AuditLog;
using AspireWebAppTemplate.Application.Contracts.Users;
using AspireWebAppTemplate.Domain.Enums;
using AspireWebAppTemplate.Infrastructure.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace AspireWebAppTemplate.ApiService.Controllers;

/// <summary>
/// Handles authentication and account self-management operations.
/// This controller is intentionally thin — it handles HTTP concerns only (request parsing,
/// status code mapping) and delegates all business logic to dedicated services.
/// </summary>
/// <remarks>
/// <para>
/// Endpoint delegation:
/// <list type="bullet">
///   <item>Login/Register/Logout/2FA-login/Recovery-login/Validate-token/Forgot-password/Reset-password/Confirm-email → <see cref="ILoginService"/> / <see cref="IRegisterService"/></item>
///   <item>Profile/Preferences/Password/Email/2FA-setup/Data/External-logins/Passkeys → <see cref="IAuthService"/></item>
/// </list>
/// </para>
/// </remarks>
[Route("api/[controller]")]
public class AuthController : BaseController
{
    #region Constructor

    private readonly IAuthService _authService;
    private readonly ILoginService _loginService;
    private readonly ILdapLoginService _ldapLoginService;
    private readonly IRegisterService _registerService;
    private readonly IAuditLogService _auditLogService;
    private readonly LdapSettings _ldapSettings;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthController"/> class.
    /// </summary>
    public AuthController(
        IAuthService authService,
        ILoginService loginService,
        ILdapLoginService ldapLoginService,
        IRegisterService registerService,
        IAuditLogService auditLogService,
        IOptions<LdapSettings> ldapSettings,
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager)
    {
        _authService = authService;
        _loginService = loginService;
        _ldapLoginService = ldapLoginService;
        _registerService = registerService;
        _auditLogService = auditLogService;
        _ldapSettings = ldapSettings.Value;
        _signInManager = signInManager;
        _userManager = userManager;
    }

    #endregion

    #region Authentication (Login, Register, Logout)

    /// <summary>
    /// Authenticates a user with email/password credentials.
    /// Uses LDAP authentication when enabled, otherwise falls back to local Identity.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoginResult>> Login([FromBody] LoginRequest request)
    {
        var ipAddress = ClientIpAddress;

        LoginResult result;
        if (_ldapSettings.Enabled)
        {
            // Try LDAP first
            result = await _ldapLoginService.ValidateAndGenerateTokenAsync(request);

            // If LDAP fails (user not in directory), fall back to local Identity
            if (!result.Succeeded && !result.IsDeactivated && !result.IsLockedOut)
            {
                result = await _loginService.ValidateAndGenerateTokenAsync(request);
            }
        }
        else
        {
            result = await _loginService.ValidateAndGenerateTokenAsync(request);
        }

        if (result.Succeeded)
        {
            await _auditLogService.LogAsync(new AuditLogRequest
            {
                UserId = result.UserId,
                ActionType = AuditActionType.LoginSuccess,
                EntityType = AuditEntityType.User,
                EntityId = result.UserId ?? "",
                EntityName = request.Email,
                Description = $"User '{request.Email}' logged in successfully.",
                IpAddress = ipAddress
            });
        }
        else
        {
            await _auditLogService.LogAsync(new AuditLogRequest
            {
                UserId = null,
                ActionType = AuditActionType.LoginFailed,
                EntityType = AuditEntityType.User,
                EntityId = request.Email,
                EntityName = request.Email,
                Description = $"Failed login attempt for '{request.Email}'. Reason: {result.ErrorMessage}",
                IpAddress = ipAddress
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Registers a new user account.
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RegisterResult>> Register([FromBody] LoginRequest request)
    {
        var registerRequest = new RegisterRequest
        {
            Email = request.Email,
            Password = request.Password,
            ConfirmEmailBaseUri = $"{Request.Scheme}://{Request.Host}/Account/ConfirmEmail",
            ReturnUrl = request.ReturnUrl
        };
        var result = await _registerService.RegisterUserAsync(registerRequest);

        // If registration succeeded and email confirmation is NOT required,
        // generate a login token so the frontend can auto-sign-in
        if (result.Succeeded && !result.RequiresEmailConfirmation)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user is not null)
            {
                var token = Guid.NewGuid().ToString("N");
                var loginData = new LoginTokenData
                {
                    UserId = user.Id,
                    RememberMe = false ,
                    ReturnUrl = request.ReturnUrl ?? "/"
                };

                var memoryCache = HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
                memoryCache.Set(
                    $"LoginToken:{token}",
                    loginData,
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
                    });

                return Ok(new RegisterResult
                {
                    Succeeded = true,
                    RequiresEmailConfirmation = false,
                    Email = request.Email,
                    Token = token
                });
            }
        }

        return Ok(result);
    }

    /// <summary>
    /// Signs the current user out and logs the event.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout()
    {
        var userId = CurrentUserId;
        var userName = CurrentUserName ?? "Unknown";
        var ipAddress = ClientIpAddress;

        await _signInManager.SignOutAsync();

        await _auditLogService.LogAsync(new AuditLogRequest
        {
            UserId = userId,
            ActionType = AuditActionType.LogoutSuccess,
            EntityType = AuditEntityType.User,
            EntityId = userId ?? "",
            EntityName = userName,
            Description = $"User '{userName}' logged out.",
            IpAddress = ipAddress
        });

        return Ok();
    }

    #endregion

    #region Profile & Preferences

    /// <summary>
    /// Returns the currently authenticated user's profile information.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDto>> Me()
    {
        try
        {
            var profile = await _authService.GetProfileAsync();
            return Ok(profile);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Updates the current user's profile information (display name, first name, last name, phone number).
    /// </summary>
    [HttpPut("profile")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        try
        {
            await _authService.UpdateProfileAsync(request);
            return Ok();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Updates the current user's display preferences (theme, timezone, date format).
    /// </summary>
    [HttpPut("preferences")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdatePreferencesRequest request)
    {
        try
        {
            await _authService.UpdatePreferencesAsync(request);
            return Ok();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    #endregion

    #region Password & Account Security

    /// <summary>
    /// Changes the password for the currently authenticated user.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            await _authService.ChangePasswordAsync(request);
            return Ok();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Sets a local password for accounts that don't have one (external/LDAP accounts).
    /// </summary>
    [HttpPost("set-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetPassword([FromBody] SetPasswordRequest request)
    {
        try
        {
            await _authService.SetPasswordAsync(request);
            return Ok();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Initiates a password reset by generating a reset token for the given email.
    /// Always returns Ok to avoid revealing whether the email exists.
    /// </summary>
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !await _userManager.IsEmailConfirmedAsync(user))
        {
            // Don't reveal that the user does not exist or is not confirmed
            return Ok();
        }

        var code = await _userManager.GeneratePasswordResetTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        // TODO: Send password reset email with the code
        // For now, just return success (email sending to be implemented)

        return Ok();
    }

    /// <summary>
    /// Resets a user's password using a previously generated reset token.
    /// </summary>
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            // Don't reveal that the user does not exist
            return Ok();
        }

        var result = await _userManager.ResetPasswordAsync(user, request.Code, request.NewPassword);
        if (result.Succeeded)
            return Ok();

        return BadRequest(string.Join(" ", result.Errors.Select(e => e.Description)));
    }

    /// <summary>
    /// Confirms a user's email address using the confirmation token.
    /// </summary>
    [HttpPost("confirm-email")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest request)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user is null)
            return NotFound("User not found.");

        var code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Code));
        var result = await _userManager.ConfirmEmailAsync(user, code);
        if (result.Succeeded)
            return Ok();

        return BadRequest("Error confirming email.");
    }

    #endregion

    #region Email Management

    /// <summary>
    /// Returns the current user's email and confirmation status.
    /// </summary>
    [HttpGet("email")]
    [Authorize]
    [ProducesResponseType(typeof(EmailInfoDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EmailInfoDto>> GetEmail()
    {
        try
        {
            var emailInfo = await _authService.GetEmailAsync();
            return Ok(emailInfo);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Initiates an email change by generating a change-email token.
    /// </summary>
    [HttpPost("change-email")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangeEmail([FromBody] ChangeEmailRequest request)
    {
        try
        {
            await _authService.ChangeEmailAsync(request);
            return Ok();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Resends the email verification/confirmation email.
    /// </summary>
    [HttpPost("send-verification-email")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SendVerificationEmail()
    {
        try
        {
            await _authService.SendVerificationEmailAsync();
            return Ok();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    #endregion

    #region Personal Data & Account

    /// <summary>
    /// Downloads the user's personal data as a JSON file.
    /// </summary>
    [HttpPost("download-personal-data")]
    [Authorize]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadPersonalData()
    {
        try
        {
            var jsonBytes = await _authService.DownloadPersonalDataAsync();
            return File(jsonBytes, "application/json", "PersonalData.json");
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Deletes the user's account after password confirmation.
    /// </summary>
    [HttpPost("delete-account")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountRequest request)
    {
        try
        {
            await _authService.DeleteAccountAsync(request);
            return Ok();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    #endregion

    #region Two-Factor Authentication

    /// <summary>
    /// Returns the current 2FA status for the authenticated user.
    /// </summary>
    [HttpGet("2fa-status")]
    [Authorize]
    [ProducesResponseType(typeof(TwoFactorStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TwoFactorStatusDto>> Get2faStatus()
    {
        try
        {
            var status = await _authService.Get2faStatusAsync();
            return Ok(status);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Returns the shared key and authenticator URI for TOTP setup.
    /// </summary>
    [HttpGet("authenticator-setup")]
    [Authorize]
    [ProducesResponseType(typeof(AuthenticatorSetupDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthenticatorSetupDto>> GetAuthenticatorSetup()
    {
        try
        {
            var setup = await _authService.GetAuthenticatorSetupAsync();
            return Ok(setup);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Verifies a TOTP code and enables 2FA for the user.
    /// </summary>
    [HttpPost("verify-authenticator")]
    [Authorize]
    [ProducesResponseType(typeof(VerifyAuthenticatorResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VerifyAuthenticatorResult>> VerifyAuthenticator([FromBody] VerifyAuthenticatorRequest request)
    {
        try
        {
            var result = await _authService.VerifyAuthenticatorAsync(request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Disables 2FA for the authenticated user.
    /// </summary>
    [HttpPost("disable-2fa")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Disable2fa()
    {
        try
        {
            await _authService.Disable2faAsync();
            return Ok();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Generates a new set of recovery codes for the authenticated user.
    /// </summary>
    [HttpPost("generate-recovery-codes")]
    [Authorize]
    [ProducesResponseType(typeof(string[]), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateRecoveryCodes()
    {
        try
        {
            var codes = await _authService.GenerateRecoveryCodesAsync();
            return Ok(codes);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Resets the authenticator key, disabling the current authenticator app.
    /// </summary>
    [HttpPost("reset-authenticator")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetAuthenticator()
    {
        try
        {
            await _authService.ResetAuthenticatorAsync();
            return Ok();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Logs in with a 2FA TOTP code.
    /// </summary>
    [HttpPost("login-2fa")]
    [ProducesResponseType(typeof(LoginResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<LoginResult>> LoginWith2fa([FromBody] LoginWith2faRequest request)
    {
        var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null)
            return Ok(new LoginResult { Succeeded = false, ErrorMessage = "Unable to load two-factor authentication user." });

        var authenticatorCode = request.Code.Replace(" ", string.Empty).Replace("-", string.Empty);
        var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(authenticatorCode, request.RememberMe, request.RememberMachine);

        if (result.Succeeded)
        {
            // Generate a single-use login token for the Web project to create a cookie
            var token = Guid.NewGuid().ToString("N");
            var memoryCache = HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
            memoryCache.Set($"LoginToken:{token}",
                new LoginTokenData { UserId = user.Id, RememberMe = request.RememberMe, ReturnUrl = "/" },
                new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2) });
            return Ok(new LoginResult { Succeeded = true, Token = token, UserId = user.Id });
        }

        if (result.IsLockedOut)
            return Ok(new LoginResult { Succeeded = false, IsLockedOut = true, ErrorMessage = "Account locked out." });

        return Ok(new LoginResult { Succeeded = false, ErrorMessage = "Invalid authenticator code." });
    }

    /// <summary>
    /// Logs in with a recovery code.
    /// </summary>
    [HttpPost("login-recovery-code")]
    [ProducesResponseType(typeof(LoginResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<LoginResult>> LoginWithRecoveryCode([FromBody] LoginWithRecoveryCodeRequest request)
    {
        var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null)
            return Ok(new LoginResult { Succeeded = false, ErrorMessage = "Unable to load two-factor authentication user." });

        var recoveryCode = request.RecoveryCode.Replace(" ", string.Empty);
        var result = await _signInManager.TwoFactorRecoveryCodeSignInAsync(recoveryCode);

        if (result.Succeeded)
        {
            var token = Guid.NewGuid().ToString("N");
            var memoryCache = HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
            memoryCache.Set($"LoginToken:{token}",
                new LoginTokenData { UserId = user.Id, RememberMe = false, ReturnUrl = "/" },
                new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2) });
            return Ok(new LoginResult { Succeeded = true, Token = token, UserId = user.Id });
        }

        if (result.IsLockedOut)
            return Ok(new LoginResult { Succeeded = false, IsLockedOut = true, ErrorMessage = "Account locked out." });

        return Ok(new LoginResult { Succeeded = false, ErrorMessage = "Invalid recovery code." });
    }

    #endregion

    #region Passkeys & External Logins

    /// <summary>
    /// Lists the user's registered passkeys.
    /// </summary>
    [HttpGet("passkeys")]
    [Authorize]
    [ProducesResponseType(typeof(List<PasskeyInfoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PasskeyInfoDto>>> GetPasskeys()
    {
        try
        {
            var passkeys = await _authService.GetPasskeysAsync();
            return Ok(passkeys);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Deletes a passkey by credential ID.
    /// </summary>
    [HttpDelete("passkeys/{credentialId}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeletePasskey(string credentialId)
    {
        try
        {
            await _authService.DeletePasskeyAsync(credentialId);
            return Ok();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Renames a passkey.
    /// </summary>
    [HttpPut("passkeys/{credentialId}/rename")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RenamePasskey(string credentialId, [FromBody] RenamePasskeyRequest request)
    {
        try
        {
            await _authService.RenamePasskeyAsync(credentialId, request);
            return Ok();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Lists the user's linked external logins.
    /// </summary>
    [HttpGet("external-logins")]
    [Authorize]
    [ProducesResponseType(typeof(ExternalLoginsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ExternalLoginsDto>> GetExternalLogins()
    {
        try
        {
            var externalLogins = await _authService.GetExternalLoginsAsync();
            return Ok(externalLogins);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Removes a linked external login.
    /// </summary>
    [HttpPost("remove-external-login")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RemoveExternalLogin([FromBody] RemoveExternalLoginRequest request)
    {
        try
        {
            await _authService.RemoveExternalLoginAsync(request);
            return Ok();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    #endregion

    #region Token Validation

    /// <summary>
    /// Validates a login token and returns the user claims needed for cookie creation.
    /// Called by the Web project's PerformLogin endpoint.
    /// </summary>
    [HttpPost("validate-token")]
    [ProducesResponseType(typeof(LoginTokenValidationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoginTokenValidationResult>> ValidateToken([FromBody] ValidateTokenRequest request)
    {
        if (string.IsNullOrEmpty(request.Token))
            return BadRequest("Token is required.");

        var memoryCache = HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
        var cacheKey = $"LoginToken:{request.Token}";

        if (!memoryCache.TryGetValue(cacheKey, out LoginTokenData? loginData) || loginData is null)
            return BadRequest("Invalid or expired token.");

        memoryCache.Remove(cacheKey);

        var user = await _userManager.FindByIdAsync(loginData.UserId);
        if (user is null)
            return BadRequest("User not found.");

        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new LoginTokenValidationResult
        {
            UserId = user.Id,
            UserName = user.UserName ?? "",
            Email = user.Email,
            DisplayName = user.DisplayName,
            Roles = roles.ToList(),
            RememberMe = loginData.RememberMe,
            ReturnUrl = loginData.ReturnUrl
        });
    }

    #endregion
}
