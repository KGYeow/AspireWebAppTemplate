using Microsoft.Extensions.Logging;
using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Application.Features.Template.Authentication;
using AspireWebAppTemplate.Application.Features.Template.Email;
using AspireWebAppTemplate.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using AspireWebAppTemplate.Infrastructure.Identity;

namespace AspireWebAppTemplate.Infrastructure.Services;

/// <summary>
/// Implements <see cref="ILoginService"/> by validating credentials using
/// <see cref="UserManager{TUser}"/> (safe on a SignalR circuit) and storing
/// a short-lived single-use token in <see cref="IMemoryCache"/> for the
/// subsequent HTTP cookie sign-in.
/// </summary>
public sealed class LoginService : ILoginService
{
    #region Constructor

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<LoginService> _logger;
    private readonly IEmailService _emailService;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginService"/> class.
    /// </summary>
    /// <param name="userManager">The user manager for credential and lockout checks.</param>
    /// <param name="signInManager">The sign-in manager for <c>CanSignInAsync</c> checks.</param>
    /// <param name="memoryCache">The memory cache for storing single-use login tokens.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="emailService">The email service for sending lockout notifications.</param>
    public LoginService(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IMemoryCache memoryCache, ILogger<LoginService> logger, IEmailService emailService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _memoryCache = memoryCache;
        _logger = logger;
        _emailService = emailService;
    }

    #endregion

    #region Operations

    /// <inheritdoc />
    public async Task<LoginResult> ValidateAndGenerateTokenAsync(LoginRequest request)
    {
        var email = request.Email;
        var password = request.Password;
        var rememberMe = request.RememberMe;
        var returnUrl = request.ReturnUrl ?? "/";

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return new LoginResult { ErrorMessage = "Invalid email or password." };
        }

        // Check lockout
        if (await _userManager.IsLockedOutAsync(user))
        {
            _logger.LogWarning("User {Email} account is locked out.", email);
            return new LoginResult { IsLockedOut = true };
        }

        // Validate password
        var passwordValid = await _userManager.CheckPasswordAsync(user, password);
        if (!passwordValid)
        {
            // Increment failed access count for lockout tracking
            await _userManager.AccessFailedAsync(user);

            // Check if this attempt caused the account to become locked out.
            // If so, send a lockout notification email (best-effort).
            if (await _userManager.IsLockedOutAsync(user))
            {
                var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
                await _emailService.TrySendEmailAsync(new TrySendEmailRequest
                {
                    UserId = user.Id,
                    RecipientEmail = user.Email,
                    Category = NotificationCategory.Account,
                    EmailType = EmailType.AccountLockout,
                    Variables = new Dictionary<string, string>
                    {
                        ["UserName"] = user.DisplayName ?? user.UserName ?? string.Empty,
                        ["LockoutEnd"] = lockoutEnd?.UtcDateTime.ToString("g") ?? "Unknown"
                    }
                });
            }

            return new LoginResult { ErrorMessage = "Invalid email or password." };
        }

        // Check if user can sign in (e.g., email confirmed)
        if (!await _signInManager.CanSignInAsync(user))
        {
            return new LoginResult { ErrorMessage = "Invalid email or password." };
        }

        // Check if user account is active
        if (!user.IsActive)
        {
            _logger.LogWarning("User {Email} account is deactivated.", email);
            return new LoginResult { IsDeactivated = true };
        }

        // Check two-factor requirement
        if (await _userManager.GetTwoFactorEnabledAsync(user))
        {
            var validProviders = await _userManager.GetValidTwoFactorProvidersAsync(user);
            if (validProviders.Count > 0)
            {
                return new LoginResult { RequiresTwoFactor = true };
            }
        }

        // Credentials valid — stamp last login and reset failed access count
        _logger.LogInformation("User {Email} logged in successfully.", email);
        await StampLastLoginAsync(user);
        await _userManager.ResetAccessFailedCountAsync(user);

        // Store a single-use token in memory cache
        var token = Guid.NewGuid().ToString("N");
        var loginData = new LoginTokenData
        {
            UserId = user.Id,
            RememberMe = rememberMe,
            ReturnUrl = returnUrl
        };

        _memoryCache.Set(
            $"LoginToken:{token}",
            loginData,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
            });

        return new LoginResult { Succeeded = true, Token = token, UserId = user.Id };
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Updates <see cref="ApplicationUser.LastLoginUtc"/> to the current UTC time.
    /// Failures are logged but do not interrupt the login flow.
    /// </summary>
    /// <param name="user">The user who just signed in.</param>
    private async Task StampLastLoginAsync(ApplicationUser user)
    {
        user.LastLoginUtc = DateTime.UtcNow;
        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "Failed to update LastLoginUtc for {Email}: {Errors}",
                user.Email,
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    #endregion
}
