using AspireWebAppTemplate.Abstractions;
using AspireWebAppTemplate.ApiService.Data;
using AspireWebAppTemplate.Core.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using AspireWebAppTemplate.ApiService.Data.Entities;

namespace AspireWebAppTemplate.Services;

/// <summary>
/// Implements <see cref="ILoginService"/> by validating credentials using
/// <see cref="UserManager{TUser}"/> (safe on a SignalR circuit) and storing
/// a short-lived single-use token in <see cref="IMemoryCache"/> for the
/// subsequent HTTP cookie sign-in.
/// </summary>
public sealed class LoginService : ILoginService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<LoginService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginService"/> class.
    /// </summary>
    /// <param name="userManager">The user manager for credential and lockout checks.</param>
    /// <param name="signInManager">The sign-in manager for <c>CanSignInAsync</c> checks.</param>
    /// <param name="memoryCache">The memory cache for storing single-use login tokens.</param>
    /// <param name="logger">The logger instance.</param>
    public LoginService(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IMemoryCache memoryCache, ILogger<LoginService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LoginResult> ValidateAndGenerateTokenAsync(string email, string password, bool rememberMe, string returnUrl)
    {
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
}
