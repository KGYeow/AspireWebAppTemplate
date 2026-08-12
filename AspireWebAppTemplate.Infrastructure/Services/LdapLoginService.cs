using Microsoft.Extensions.Logging;
using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Domain.Enums;
using AspireWebAppTemplate.Application.Contracts.Auth;
using AspireWebAppTemplate.Application.Contracts.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using AspireWebAppTemplate.Infrastructure.Identity;

namespace AspireWebAppTemplate.Infrastructure.Services;

/// <summary>
/// Implements <see cref="ILdapLoginService"/> by orchestrating LDAP authentication,
/// auto-provisioning users in the local Identity database, syncing attributes from AD,
/// and generating a single-use login token for cookie sign-in.
/// </summary>
/// <remarks>
/// [LDAP] This service is part of the LDAP integration. Remove it if LDAP is not needed.
/// </remarks>
public sealed class LdapLoginService : ILdapLoginService
{
    #region Constructor

    private readonly ILdapAuthService _ldapAuth;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<LdapLoginService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LdapLoginService"/> class.
    /// </summary>
    public LdapLoginService(ILdapAuthService ldapAuth, UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager, IMemoryCache memoryCache, ILogger<LdapLoginService> logger)
    {
        _ldapAuth = ldapAuth;
        _userManager = userManager;
        _roleManager = roleManager;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    #endregion

    #region Operations

    /// <inheritdoc />
    public async Task<LoginResult> ValidateAndGenerateTokenAsync(LoginRequest request)
    {
        var identifier = request.Email;
        var password = request.Password;
        var rememberMe = request.RememberMe;
        var returnUrl = request.ReturnUrl ?? "/";

        // Authenticate against LDAP
        var ldapResult = await _ldapAuth.AuthenticateAsync(identifier, password);
        if (!ldapResult.Succeeded || ldapResult.Attributes is null)
        {
            return new LoginResult { ErrorMessage = ldapResult.ErrorMessage ?? "LDAP authentication failed." };
        }

        var attrs = ldapResult.Attributes;

        // Find or auto-provision user in local Identity database
        var user = await _userManager.FindByNameAsync(attrs.Ntid) ?? await _userManager.FindByEmailAsync(attrs.Email);

        if (user is null)
        {
            // Auto-provision new user
            user = new ApplicationUser
            {
                UserName = attrs.Ntid,
                Email = attrs.Email,
                EmailConfirmed = true,
                DisplayName = attrs.DisplayName,
                FirstName = attrs.FirstName,
                LastName = attrs.LastName,
                JobTitle = attrs.JobTitle,
                Department = attrs.Department,
                EmployeeNumber = attrs.EmployeeNumber,
                IsActive = true,
                AuthSource = AuthSource.LDAP,
                CreatedUtc = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                _logger.LogWarning("Failed to auto-provision LDAP user {Ntid}: {Errors}",
                    attrs.Ntid, string.Join(", ", createResult.Errors.Select(e => e.Description)));
                return new LoginResult { ErrorMessage = "Failed to create local account." };
            }

            // Assign the default role (IsDefault = true), falling back to "User" if none is marked
            var defaultRoleName = _roleManager.Roles.FirstOrDefault(r => r.IsDefault)?.Name ?? "User";
            var roleResult = await _userManager.AddToRoleAsync(user, defaultRoleName);
            if (!roleResult.Succeeded)
            {
                _logger.LogWarning("Failed to assign default role to LDAP user {Ntid}: {Errors}",
                    attrs.Ntid, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
            }

            _logger.LogInformation("Auto-provisioned LDAP user {Ntid} ({DisplayName}).", attrs.Ntid, attrs.DisplayName);
        }
        else
        {
            // Sync attributes from LDAP to local user
            await SyncUserAttributesAsync(user, attrs);
        }

        // Check if user is active
        if (!user.IsActive)
        {
            _logger.LogWarning("LDAP user {Ntid} account is deactivated.", attrs.Ntid);
            return new LoginResult { IsDeactivated = true };
        }

        // Stamp last login
        user.LastLoginUtc = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("LDAP user {Ntid} logged in successfully.", attrs.Ntid);

        // Generate single-use token (same pattern as LoginService)
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
    /// Syncs user attributes from LDAP to the local Identity user if any have changed.
    /// </summary>
    private async Task SyncUserAttributesAsync(ApplicationUser user, LdapUserAttributes attrs)
    {
        bool changed = false;

        if (!string.Equals(user.DisplayName, attrs.DisplayName, StringComparison.Ordinal))
        {
            user.DisplayName = attrs.DisplayName;
            changed = true;
        }

        if (!string.Equals(user.FirstName, attrs.FirstName, StringComparison.Ordinal))
        {
            user.FirstName = attrs.FirstName;
            changed = true;
        }

        if (!string.Equals(user.LastName, attrs.LastName, StringComparison.Ordinal))
        {
            user.LastName = attrs.LastName;
            changed = true;
        }

        if (!string.Equals(user.Email, attrs.Email, StringComparison.OrdinalIgnoreCase))
        {
            user.Email = attrs.Email;
            changed = true;
        }

        if (!string.Equals(user.JobTitle, attrs.JobTitle, StringComparison.Ordinal))
        {
            user.JobTitle = attrs.JobTitle;
            changed = true;
        }

        if (!string.Equals(user.Department, attrs.Department, StringComparison.Ordinal))
        {
            user.Department = attrs.Department;
            changed = true;
        }

        if (!string.Equals(user.EmployeeNumber, attrs.EmployeeNumber, StringComparison.Ordinal))
        {
            user.EmployeeNumber = attrs.EmployeeNumber;
            changed = true;
        }

        if (changed)
        {
            user.UpdatedUtc = DateTime.UtcNow;
            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                _logger.LogInformation("Synced LDAP attributes for user {Ntid}.", attrs.Ntid);
            }
            else
            {
                _logger.LogWarning("Failed to sync LDAP attributes for {Ntid}: {Errors}",
                    attrs.Ntid, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    #endregion
}
