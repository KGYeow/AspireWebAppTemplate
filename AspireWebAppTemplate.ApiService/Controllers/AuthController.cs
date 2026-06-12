using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using AspireWebAppTemplate.Abstractions;
using AspireWebAppTemplate.ApiService.Data.Entities;
using AspireWebAppTemplate.Core.Contracts;
using AspireWebAppTemplate.Core.Domain.Enums;
using AspireWebAppTemplate.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace AspireWebAppTemplate.Controllers;

/// <summary>
/// Handles authentication operations including login, logout, registration, and password management.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    #region Constructor

    private readonly ILoginService _loginService;
    private readonly ILdapLoginService _ldapLoginService;
    private readonly IRegisterService _registerService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IAuditLogService _auditLogService;
    private readonly LdapSettings _ldapSettings;

    public AuthController(
        ILoginService loginService,
        ILdapLoginService ldapLoginService,
        IRegisterService registerService,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IAuditLogService auditLogService,
        IOptions<LdapSettings> ldapSettings)
    {
        _loginService = loginService;
        _ldapLoginService = ldapLoginService;
        _registerService = registerService;
        _userManager = userManager;
        _signInManager = signInManager;
        _auditLogService = auditLogService;
        _ldapSettings = ldapSettings.Value;
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
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        LoginResult result;
        if (_ldapSettings.Enabled)
        {
            // Try LDAP first
            result = await _ldapLoginService.ValidateAndGenerateTokenAsync(
                request.Email, request.Password, request.RememberMe, request.ReturnUrl ?? "/");

            // If LDAP fails (user not in directory), fall back to local Identity
            if (!result.Succeeded && !result.IsDeactivated && !result.IsLockedOut)
            {
                result = await _loginService.ValidateAndGenerateTokenAsync(
                    request.Email, request.Password, request.RememberMe, request.ReturnUrl ?? "/");
            }
        }
        else
        {
            result = await _loginService.ValidateAndGenerateTokenAsync(
                request.Email, request.Password, request.RememberMe, request.ReturnUrl ?? "/");
        }

        if (result.Succeeded)
        {
            await _auditLogService.LogAsync(
                result.UserId,
                AuditActionType.LoginSuccess,
                AuditEntityType.User,
                result.UserId ?? "",
                request.Email,
                $"User '{request.Email}' logged in successfully.",
                ipAddress: ipAddress);
        }
        else
        {
            await _auditLogService.LogAsync(
                null,
                AuditActionType.LoginFailed,
                AuditEntityType.User,
                request.Email,
                request.Email,
                $"Failed login attempt for '{request.Email}'. Reason: {result.ErrorMessage}",
                ipAddress: ipAddress);
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
        var baseUri = $"{Request.Scheme}://{Request.Host}/Account/ConfirmEmail";
        var result = await _registerService.RegisterUserAsync(
            request.Email, request.Password, baseUri, null);

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
                    RememberMe = false,
                    ReturnUrl = request.ReturnUrl ?? "/"
                };

                var memoryCache = HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                memoryCache.Set(
                    $"LoginToken:{token}",
                    loginData,
                    new Microsoft.Extensions.Caching.Memory.MemoryCacheEntryOptions
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
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = User.Identity?.Name ?? "Unknown";
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        await _signInManager.SignOutAsync();

        await _auditLogService.LogAsync(
            userId,
            AuditActionType.LogoutSuccess,
            AuditEntityType.User,
            userId ?? "",
            userName,
            $"User '{userName}' logged out.",
            ipAddress: ipAddress);

        return Ok();
    }

    #endregion

    #region User Profile + Password

    /// <summary>
    /// Changes the password for the currently authenticated user.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return NotFound("User not found.");

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return BadRequest(errors);
        }

        user.LastPasswordChangeUtc = DateTime.UtcNow;
        user.UpdatedUtc = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditLogService.LogAsync(
            userId,
            AuditActionType.PasswordChanged,
            AuditEntityType.User,
            userId,
            user.DisplayName ?? user.UserName ?? "Unknown",
            $"User '{user.DisplayName ?? user.UserName}' changed their password.",
            ipAddress: ipAddress);

        return Ok();
    }

    /// <summary>
    /// Returns the currently authenticated user's profile information.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return NotFound();

        var roles = await _userManager.GetRolesAsync(user);

        var dto = new UserDto
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
            DateTimeFormat = user.DateTimeFormat
        };

        return Ok(dto);
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
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return NotFound("User not found.");

        if (request.Theme.HasValue)
            user.Theme = request.Theme.Value;
        if (request.TimeZoneId is not null)
            user.TimeZoneId = request.TimeZoneId;
        if (request.DateTimeFormat is not null)
            user.DateTimeFormat = request.DateTimeFormat;

        user.UpdatedUtc = DateTime.UtcNow;
        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
            return BadRequest(string.Join("; ", result.Errors.Select(e => e.Description)));

        return Ok();
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
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return NotFound("User not found.");

        if (await _userManager.HasPasswordAsync(user))
            return BadRequest("User already has a password. Use change-password instead.");

        var result = await _userManager.AddPasswordAsync(user, request.NewPassword);
        if (!result.Succeeded)
            return BadRequest(string.Join("; ", result.Errors.Select(e => e.Description)));

        return Ok();
    }

    /// <summary>
    /// Updates the current user's profile information (phone number, etc.).
    /// </summary>
    [HttpPut("profile")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return NotFound("User not found.");

        if (request.DisplayName is not null)
            user.DisplayName = request.DisplayName;
        if (request.FirstName is not null)
            user.FirstName = request.FirstName;
        if (request.LastName is not null)
            user.LastName = request.LastName;

        if (request.PhoneNumber is not null)
        {
            var setPhoneResult = await _userManager.SetPhoneNumberAsync(user, request.PhoneNumber);
            if (!setPhoneResult.Succeeded)
                return BadRequest(string.Join("; ", setPhoneResult.Errors.Select(e => e.Description)));
        }

        user.UpdatedUtc = DateTime.UtcNow;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return BadRequest(string.Join("; ", result.Errors.Select(e => e.Description)));

        return Ok();
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
        {
            return Ok();
        }

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
        {
            return NotFound("User not found.");
        }

        var code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Code));
        var result = await _userManager.ConfirmEmailAsync(user, code);
        if (result.Succeeded)
        {
            return Ok();
        }

        return BadRequest("Error confirming email.");
    }

    #endregion

    #region Email Management + Personal Data

    /// <summary>
    /// Returns the current user's email and confirmation status.
    /// </summary>
    [HttpGet("email")]
    [Authorize]
    [ProducesResponseType(typeof(EmailInfoDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EmailInfoDto>> GetEmail()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        return Ok(new EmailInfoDto
        {
            Email = user.Email ?? "",
            IsEmailConfirmed = user.EmailConfirmed
        });
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
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        if (string.Equals(user.Email, request.NewEmail, StringComparison.OrdinalIgnoreCase))
            return BadRequest("The new email is the same as the current email.");

        var code = await _userManager.GenerateChangeEmailTokenAsync(user, request.NewEmail);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        // TODO: Send email change confirmation email with the code
        // For now, apply the change directly (in production, confirm via email link)
        var decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        var result = await _userManager.ChangeEmailAsync(user, request.NewEmail, decodedCode);
        if (!result.Succeeded)
            return BadRequest(string.Join("; ", result.Errors.Select(e => e.Description)));

        // Also update username to match email
        await _userManager.SetUserNameAsync(user, request.NewEmail);

        return Ok();
    }

    /// <summary>
    /// Resends the email verification/confirmation email.
    /// </summary>
    [HttpPost("send-verification-email")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SendVerificationEmail()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        // TODO: Send the verification email with the code
        // For now, just return success
        return Ok();
    }

    /// <summary>
    /// Downloads the user's personal data as a JSON file.
    /// </summary>
    [HttpPost("download-personal-data")]
    [Authorize]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadPersonalData()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        // Collect all personal data
        var personalData = new Dictionary<string, string>();
        var personalDataProps = typeof(ApplicationUser).GetProperties()
            .Where(prop => Attribute.IsDefined(prop, typeof(PersonalDataAttribute)));

        foreach (var p in personalDataProps)
        {
            personalData.Add(p.Name, p.GetValue(user)?.ToString() ?? "null");
        }

        // Add Identity-specific data
        personalData.Add("Id", user.Id);
        personalData.Add("UserName", user.UserName ?? "");
        personalData.Add("Email", user.Email ?? "");
        personalData.Add("EmailConfirmed", user.EmailConfirmed.ToString());
        personalData.Add("PhoneNumber", user.PhoneNumber ?? "");
        personalData.Add("PhoneNumberConfirmed", user.PhoneNumberConfirmed.ToString());
        personalData.Add("TwoFactorEnabled", user.TwoFactorEnabled.ToString());

        var logins = await _userManager.GetLoginsAsync(user);
        foreach (var login in logins)
        {
            personalData.Add($"{login.LoginProvider} external login provider key", login.ProviderKey);
        }

        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(personalData, new JsonSerializerOptions { WriteIndented = true });
        return File(jsonBytes, "application/json", "PersonalData.json");
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
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        if (await _userManager.HasPasswordAsync(user))
        {
            if (string.IsNullOrEmpty(request.Password))
                return BadRequest("Password is required.");

            if (!await _userManager.CheckPasswordAsync(user, request.Password))
                return BadRequest("Incorrect password.");
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
            return BadRequest(string.Join("; ", result.Errors.Select(e => e.Description)));

        await _signInManager.SignOutAsync();

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditLogService.LogAsync(
            userId,
            AuditActionType.UserDeleted,
            AuditEntityType.User,
            userId,
            user.DisplayName ?? user.UserName ?? "Unknown",
            $"User '{user.DisplayName ?? user.UserName}' deleted their account.",
            ipAddress: ipAddress);

        return Ok();
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
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        var dto = new TwoFactorStatusDto
        {
            HasAuthenticator = await _userManager.GetAuthenticatorKeyAsync(user) is not null,
            Is2faEnabled = await _userManager.GetTwoFactorEnabledAsync(user),
            IsMachineRemembered = await _signInManager.IsTwoFactorClientRememberedAsync(user),
            RecoveryCodesLeft = await _userManager.CountRecoveryCodesAsync(user)
        };

        return Ok(dto);
    }

    /// <summary>
    /// Returns the shared key and authenticator URI for TOTP setup.
    /// </summary>
    [HttpGet("authenticator-setup")]
    [Authorize]
    [ProducesResponseType(typeof(AuthenticatorSetupDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthenticatorSetupDto>> GetAuthenticatorSetup()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(unformattedKey))
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        var sharedKey = FormatKey(unformattedKey!);
        var email = await _userManager.GetEmailAsync(user);
        var authenticatorUri = GenerateQrCodeUri(email!, unformattedKey!);

        return Ok(new AuthenticatorSetupDto
        {
            SharedKey = sharedKey,
            AuthenticatorUri = authenticatorUri
        });
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
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        var verificationCode = request.Code.Replace(" ", string.Empty).Replace("-", string.Empty);

        var is2faTokenValid = await _userManager.VerifyTwoFactorTokenAsync(
            user, _userManager.Options.Tokens.AuthenticatorTokenProvider, verificationCode);

        if (!is2faTokenValid)
        {
            return Ok(new VerifyAuthenticatorResult { Succeeded = false });
        }

        await _userManager.SetTwoFactorEnabledAsync(user, true);
        var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);

        return Ok(new VerifyAuthenticatorResult
        {
            Succeeded = true,
            RecoveryCodes = recoveryCodes?.ToArray()
        });
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
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        if (!await _userManager.GetTwoFactorEnabledAsync(user))
            return BadRequest("Cannot disable 2FA as it is not currently enabled.");

        var result = await _userManager.SetTwoFactorEnabledAsync(user, false);
        if (!result.Succeeded)
            return BadRequest(string.Join("; ", result.Errors.Select(e => e.Description)));

        return Ok();
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
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        if (!await _userManager.GetTwoFactorEnabledAsync(user))
            return BadRequest("Cannot generate recovery codes as 2FA is not enabled.");

        var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        return Ok(recoveryCodes?.ToArray() ?? []);
    }

    /// <summary>
    /// Resets the authenticator key, disabling the current authenticator app.
    /// </summary>
    [HttpPost("reset-authenticator")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetAuthenticator()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        await _userManager.SetTwoFactorEnabledAsync(user, false);
        await _userManager.ResetAuthenticatorKeyAsync(user);

        await _signInManager.RefreshSignInAsync(user);

        return Ok();
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
        {
            return Ok(new LoginResult { Succeeded = false, ErrorMessage = "Unable to load two-factor authentication user." });
        }

        var authenticatorCode = request.Code.Replace(" ", string.Empty).Replace("-", string.Empty);

        var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(authenticatorCode, request.RememberMe, request.RememberMachine);

        if (result.Succeeded)
        {
            return Ok(new LoginResult { Succeeded = true, UserId = user.Id });
        }
        else if (result.IsLockedOut)
        {
            return Ok(new LoginResult { Succeeded = false, IsLockedOut = true, ErrorMessage = "Account locked out." });
        }
        else
        {
            return Ok(new LoginResult { Succeeded = false, ErrorMessage = "Invalid authenticator code." });
        }
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
        {
            return Ok(new LoginResult { Succeeded = false, ErrorMessage = "Unable to load two-factor authentication user." });
        }

        var recoveryCode = request.RecoveryCode.Replace(" ", string.Empty);
        var result = await _signInManager.TwoFactorRecoveryCodeSignInAsync(recoveryCode);

        if (result.Succeeded)
        {
            return Ok(new LoginResult { Succeeded = true, UserId = user.Id });
        }
        else if (result.IsLockedOut)
        {
            return Ok(new LoginResult { Succeeded = false, IsLockedOut = true, ErrorMessage = "Account locked out." });
        }
        else
        {
            return Ok(new LoginResult { Succeeded = false, ErrorMessage = "Invalid recovery code." });
        }
    }

    #endregion

    #region Passkeys + External Logins

    /// <summary>
    /// Lists the user's registered passkeys.
    /// </summary>
    [HttpGet("passkeys")]
    [Authorize]
    [ProducesResponseType(typeof(List<PasskeyInfoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PasskeyInfoDto>>> GetPasskeys()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        // Passkey support requires WebAuthn credential storage.
        // Return empty list as stub — full WebAuthn integration to be wired later.
        return Ok(new List<PasskeyInfoDto>());
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
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        // Stub: Passkey deletion requires WebAuthn credential storage.
        // Full implementation to be added when WebAuthn storage is available.
        return Ok();
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
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        // Stub: Passkey rename requires WebAuthn credential storage.
        // Full implementation to be added when WebAuthn storage is available.
        return Ok();
    }

    /// <summary>
    /// Lists the user's linked external logins.
    /// </summary>
    [HttpGet("external-logins")]
    [Authorize]
    [ProducesResponseType(typeof(ExternalLoginsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ExternalLoginsDto>> GetExternalLogins()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        var currentLogins = await _userManager.GetLoginsAsync(user);
        var hasPassword = await _userManager.HasPasswordAsync(user);

        var dto = new ExternalLoginsDto
        {
            CurrentLogins = currentLogins.Select(l => new ExternalLoginInfoDto
            {
                LoginProvider = l.LoginProvider,
                ProviderDisplayName = l.ProviderDisplayName ?? l.LoginProvider,
                ProviderKey = l.ProviderKey
            }).ToList(),
            ShowRemoveButton = hasPassword || currentLogins.Count > 1
        };

        return Ok(dto);
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
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        var result = await _userManager.RemoveLoginAsync(user, request.LoginProvider, request.ProviderKey);
        if (!result.Succeeded)
            return BadRequest(string.Join("; ", result.Errors.Select(e => e.Description)));

        await _signInManager.RefreshSignInAsync(user);
        return Ok();
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

        var memoryCache = HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
        var cacheKey = $"LoginToken:{request.Token}";

        if (!memoryCache.TryGetValue(cacheKey, out LoginTokenData? loginData) || loginData is null)
            return BadRequest("Invalid or expired token.");

        // Remove the token (single-use)
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

    #region Helpers

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

    #endregion
}
