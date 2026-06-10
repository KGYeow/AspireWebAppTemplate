using System.ComponentModel.DataAnnotations;
using BlazorWebAppTemplate.Abstractions;
using BlazorWebAppTemplate.Core.Domain.Enums;
using BlazorWebAppTemplate.Data;
using BlazorWebAppTemplate.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Identity;

namespace BlazorWebAppTemplate.Components.Account.Pages.Manage;

/// <summary>
/// Set password page for users who registered via external login and have no local password.
/// Uses <c>InteractiveServer</c> render mode.
/// </summary>
public partial class SetPassword : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// Manages user accounts. Used to add a local password.
    /// </summary>
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    /// <summary>
    /// Provides navigation actions.
    /// </summary>
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    /// <summary>
    /// Audit log service for recording the password set event.
    /// </summary>
    [Inject] private IAuditLogService AuditLogService { get; set; } = default!;

    #endregion

    #region Cascading Parameters

    /// <summary>
    /// Provides the current authentication state to resolve the user.
    /// </summary>
    [CascadingParameter]
    private Task<AuthenticationState> AuthStateTask { get; set; } = default!;

    #endregion

    #region State

    /// <summary>
    /// The current user.
    /// </summary>
    private ApplicationUser? user;

    /// <summary>
    /// The form input model.
    /// </summary>
    private InputModel Input { get; set; } = new();

    /// <summary>
    /// Drives the <c>EditForm</c> validation context.
    /// </summary>
    private EditContext editContext = default!;

    /// <summary>
    /// Status message displayed after save.
    /// </summary>
    protected string? StatusMessage { get; set; }

    /// <summary>
    /// Controls the submit button's disabled state and loading spinner.
    /// </summary>
    protected bool IsBusy { get; set; }

    /// <summary>
    /// Toggles the new password field visibility.
    /// </summary>
    protected bool IsPasswordVisible { get; set; }

    /// <summary>
    /// Toggles the confirm password field visibility.
    /// </summary>
    protected bool IsConfirmVisible { get; set; }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Initializes the edit context synchronously.
    /// </summary>
    protected override void OnInitialized()
    {
        editContext = new EditContext(Input);
    }

    /// <summary>
    /// Loads the current user and redirects to ChangePassword if a local password already exists.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateTask;
        user = await UserManager.GetUserAsync(authState.User);
        if (user is null)
        {
            NavigationManager.NavigateTo("Account/InvalidUser", forceLoad: true);
            return;
        }

        if (await UserManager.HasPasswordAsync(user))
        {
            NavigationManager.NavigateTo("Account/Manage/ChangePassword", forceLoad: true);
        }
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Adds a local password to the user's account on valid form submission.
    /// </summary>
    protected async Task OnValidSubmitAsync()
    {
        if (IsBusy || user is null) return;
        IsBusy = true;
        StatusMessage = null;

        try
        {
            var result = await UserManager.AddPasswordAsync(user, Input.NewPassword!);
            if (!result.Succeeded)
            {
                StatusMessage = $"Error: {string.Join(" ", result.Errors.Select(e => e.Description))}";
                return;
            }

            // Audit log: record password set event (fire-and-forget safe, won't interrupt primary operation)
            await AuditLogService.LogAsync(
                userId: user.Id,
                actionType: AuditActionType.PasswordChanged,
                entityType: AuditEntityType.User,
                entityId: user.Id,
                entityName: user.DisplayName ?? user.UserName ?? user.Id,
                description: "User set a local password for their account.");

            StatusMessage = "Your password has been set.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    #region Input Model

    /// <summary>
    /// Form model for the set password form.
    /// </summary>
    private sealed class InputModel
    {
        /// <summary>
        /// The user's new password.
        /// </summary>
        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string? NewPassword { get; set; }

        /// <summary>
        /// Confirmation of the new password. Must match <see cref="NewPassword"/>.
        /// </summary>
        [DataType(DataType.Password)]
        [Display(Name = "Confirm new password")]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        public string? ConfirmPassword { get; set; }
    }

    #endregion
}
