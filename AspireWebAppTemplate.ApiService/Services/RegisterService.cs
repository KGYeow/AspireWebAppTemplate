using System.Text;
using System.Text.Encodings.Web;
using AspireWebAppTemplate.Abstractions;
using AspireWebAppTemplate.ApiService.Data;
using AspireWebAppTemplate.Core.Contracts;
using AspireWebAppTemplate.Core.Contracts.Auth;
using AspireWebAppTemplate.Core.Contracts.AuditLog;
using AspireWebAppTemplate.Core.Contracts.Roles;
using AspireWebAppTemplate.Core.Contracts.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using AspireWebAppTemplate.ApiService.Data.Entities;

namespace AspireWebAppTemplate.ApiService.Services;

/// <summary>
/// Implements <see cref="IRegisterService"/> by creating a new user via
/// <see cref="UserManager{TUser}"/>, assigning the default role (marked with <c>IsDefault = true</c>),
/// and sending an email confirmation link.
/// </summary>
public sealed class RegisterService : IRegisterService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IUserStore<ApplicationUser> _userStore;
    private readonly IEmailSender<ApplicationUser> _emailSender;
    private readonly ILogger<RegisterService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterService"/> class.
    /// </summary>
    /// <param name="userManager">The user manager for account creation and token generation.</param>
    /// <param name="roleManager">The role manager for querying the default role.</param>
    /// <param name="userStore">The user store for setting username and email.</param>
    /// <param name="emailSender">The email sender for confirmation emails.</param>
    /// <param name="logger">The logger instance.</param>
    public RegisterService(UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager, IUserStore<ApplicationUser> userStore, IEmailSender<ApplicationUser> emailSender, ILogger<RegisterService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _userStore = userStore;
        _emailSender = emailSender;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<RegisterResult> RegisterUserAsync(string email, string password, string confirmEmailBaseUri, string? returnUrl)
    {
        var user = CreateUser();

        await _userStore.SetUserNameAsync(user, email, CancellationToken.None);
        var emailStore = GetEmailStore();
        await emailStore.SetEmailAsync(user, email, CancellationToken.None);

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            return new RegisterResult
            {
                ErrorMessage = string.Join(" ", result.Errors.Select(e => e.Description))
            };
        }

        _logger.LogInformation("User {Email} created a new account with password.", email);

        // Assign the default role (IsDefault = true), falling back to "User" if none is marked
        var defaultRoleName = _roleManager.Roles.FirstOrDefault(r => r.IsDefault)?.Name ?? "User";
        var roleResult = await _userManager.AddToRoleAsync(user, defaultRoleName);
        if (!roleResult.Succeeded)
        {
            _logger.LogWarning(
                "Failed to assign default role '{Role}' to {Email}: {Errors}",
                defaultRoleName,
                email,
                string.Join(", ", roleResult.Errors.Select(e => e.Description)));
        }

        // Generate email confirmation token and send
        var userId = await _userManager.GetUserIdAsync(user);
        var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        var callbackUrl = BuildCallbackUrl(confirmEmailBaseUri, userId, code, returnUrl);
        await _emailSender.SendConfirmationLinkAsync(user, email, HtmlEncoder.Default.Encode(callbackUrl));

        return new RegisterResult
        {
            Succeeded = true,
            RequiresEmailConfirmation = _userManager.Options.SignIn.RequireConfirmedAccount,
            Email = email
        };
    }

    /// <summary>
    /// Builds the email confirmation callback URL with query parameters.
    /// </summary>
    /// <param name="baseUri">The absolute URI to the <c>Account/ConfirmEmail</c> page.</param>
    /// <param name="userId">The new user's ID.</param>
    /// <param name="code">The Base64Url-encoded email confirmation token.</param>
    /// <param name="returnUrl">Optional return URL.</param>
    /// <returns>The fully constructed callback URL.</returns>
    private static string BuildCallbackUrl(string baseUri, string userId, string code, string? returnUrl)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["userId"] = userId,
            ["code"] = code,
            ["returnUrl"] = returnUrl
        };

        // Build query string manually to avoid NavigationManager dependency
        var queryString = string.Join("&",
            parameters
                .Where(p => p.Value is not null)
                .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!.ToString()!)}"));

        return $"{baseUri}?{queryString}";
    }

    /// <summary>
    /// Creates a new <see cref="ApplicationUser"/> instance.
    /// </summary>
    private static ApplicationUser CreateUser()
    {
        try
        {
            return Activator.CreateInstance<ApplicationUser>();
        }
        catch
        {
            throw new InvalidOperationException(
                $"Can't create an instance of '{nameof(ApplicationUser)}'. " +
                $"Ensure that '{nameof(ApplicationUser)}' is not an abstract class and has a parameterless constructor.");
        }
    }

    /// <summary>
    /// Gets the email store from the user store.
    /// </summary>
    private IUserEmailStore<ApplicationUser> GetEmailStore()
    {
        if (!_userManager.SupportsUserEmail)
        {
            throw new NotSupportedException("The default UI requires a user store with email support.");
        }
        return (IUserEmailStore<ApplicationUser>)_userStore;
    }
}
