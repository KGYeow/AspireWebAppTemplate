using BlazorWebAppTemplate.Abstractions;
using BlazorWebAppTemplate.Core.Domain.Enums;
using BlazorWebAppTemplate.Data;
using BlazorWebAppTemplate.Data.Entities;
using BlazorWebAppTemplate.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Identity;
using MudBlazor;

namespace BlazorWebAppTemplate.Components.Pages.UserManagement;

/// <summary>
/// [LDAP] Dialog for adding a user from the corporate Active Directory.
/// Looks up the user via LDAP, shows a preview, and creates the local Identity user.
/// Remove this file if LDAP is not needed.
/// </summary>
public partial class AddLdapUserDialog : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// [LDAP] LDAP authentication service for directory lookups.
    /// </summary>
    [Inject] private ILdapAuthService LdapAuthService { get; set; } = default!;

    /// <summary>
    /// Manages user accounts for local Identity operations.
    /// </summary>
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    /// <summary>
    /// Manages roles for querying the default role.
    /// </summary>
    [Inject] private RoleManager<ApplicationRole> RoleManager { get; set; } = default!;

    /// <summary>
    /// Audit log service for recording user creation events.
    /// </summary>
    [Inject] private IAuditLogService AuditLogService { get; set; } = default!;

    #endregion

    #region Cascading Parameters

    /// <summary>
    /// The MudBlazor dialog instance.
    /// </summary>
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    /// <summary>
    /// Provides the current authentication state for identifying the acting user.
    /// </summary>
    [CascadingParameter]
    private Task<AuthenticationState> AuthStateTask { get; set; } = default!;

    #endregion

    #region State

    /// <summary>
    /// The NTID or email entered by the admin.
    /// </summary>
    protected string Identifier { get; set; } = string.Empty;

    /// <summary>
    /// The LDAP user attributes from the preview lookup.
    /// Null until a successful lookup is performed.
    /// </summary>
    protected LdapUserAttributes? PreviewAttributes { get; private set; }

    /// <summary>
    /// Error message displayed in the alert.
    /// </summary>
    protected string? ErrorMessage { get; set; }

    /// <summary>
    /// Informational message (e.g., "User found in LDAP").
    /// </summary>
    protected string? InfoMessage { get; set; }

    /// <summary>
    /// Controls the button disabled states and loading spinner.
    /// </summary>
    protected bool IsBusy { get; set; }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles Enter key press on the NTID field to trigger lookup.
    /// </summary>
    protected async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !IsBusy)
        {
            await LookupAsync();
        }
    }

    /// <summary>
    /// Looks up the user from the corporate directory via LDAP.
    /// Populates <see cref="PreviewAttributes"/> on success.
    /// </summary>
    protected async Task LookupAsync()
    {
        ErrorMessage = null;
        InfoMessage = null;
        PreviewAttributes = null;

        if (string.IsNullOrWhiteSpace(Identifier))
        {
            ErrorMessage = "Please enter NTID or email.";
            return;
        }

        IsBusy = true;
        try
        {
            var attributes = await LdapAuthService.FetchUserAttributesAsync(Identifier.Trim());
            if (attributes is null)
            {
                ErrorMessage = "User not found in corporate directory.";
                return;
            }

            PreviewAttributes = attributes;
            InfoMessage = "User found in LDAP.";
        }
        catch (Exception)
        {
            ErrorMessage = "Unexpected error during LDAP lookup.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Creates the local Identity user from the previewed LDAP attributes.
    /// Assigns the default "User" role. No password is set (LDAP auth).
    /// </summary>
    protected async Task AddUserAsync()
    {
        if (PreviewAttributes is null) return;

        ErrorMessage = null;
        InfoMessage = null;
        IsBusy = true;

        try
        {
            // Check for duplicate by username
            var existingByName = await UserManager.FindByNameAsync(PreviewAttributes.Ntid);
            if (existingByName is not null)
            {
                ErrorMessage = $"User '{PreviewAttributes.Ntid}' already exists.";
                return;
            }

            // Check for duplicate by email
            if (!string.IsNullOrEmpty(PreviewAttributes.Email))
            {
                var existingByEmail = await UserManager.FindByEmailAsync(PreviewAttributes.Email);
                if (existingByEmail is not null)
                {
                    ErrorMessage = $"A user with email '{PreviewAttributes.Email}' already exists.";
                    return;
                }
            }

            // Create the user with LDAP attributes (no password — LDAP auth)
            var user = new ApplicationUser
            {
                UserName = PreviewAttributes.Ntid,
                Email = PreviewAttributes.Email,
                EmailConfirmed = true,
                DisplayName = PreviewAttributes.DisplayName,
                FirstName = PreviewAttributes.FirstName,
                LastName = PreviewAttributes.LastName,
                JobTitle = PreviewAttributes.JobTitle,
                Department = PreviewAttributes.Department,
                EmployeeNumber = PreviewAttributes.EmployeeNumber,
                IsActive = true,
                AuthSource = AuthSource.LDAP,
                CreatedUtc = DateTime.UtcNow
            };

            var createResult = await UserManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                ErrorMessage = "Failed to create user: " +
                               string.Join(", ", createResult.Errors.Select(e => e.Description));
                return;
            }

            // Assign the default role (IsDefault = true), falling back to "User" if none is marked
            var defaultRoleName = RoleManager.Roles.FirstOrDefault(r => r.IsDefault)?.Name ?? "User";
            var roleResult = await UserManager.AddToRoleAsync(user, defaultRoleName);
            if (!roleResult.Succeeded)
            {
                ErrorMessage = $"User created, but failed to assign default role: " +
                               string.Join(", ", roleResult.Errors.Select(e => e.Description));
                return;
            }

            // Audit: log user creation event (fire-and-forget safe — failures won't interrupt)
            try
            {
                var authState = await AuthStateTask;
                var actingUserName = authState.User.Identity?.Name;
                string? actingUserId = null;
                if (actingUserName is not null)
                {
                    var actingUser = await UserManager.FindByNameAsync(actingUserName);
                    actingUserId = actingUser?.Id;
                }

                await AuditLogService.LogAsync(
                    actingUserId,
                    AuditActionType.UserCreated,
                    AuditEntityType.User,
                    user.Id,
                    user.DisplayName ?? user.UserName ?? "",
                    $"LDAP user '{user.DisplayName ?? user.UserName}' created.");
            }
            catch { /* audit failures must not interrupt the primary operation */ }

            // Close dialog with success
            MudDialog.Close(DialogResult.Ok(true));
        }
        catch (Exception)
        {
            ErrorMessage = "Unexpected error while adding user.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion
}
