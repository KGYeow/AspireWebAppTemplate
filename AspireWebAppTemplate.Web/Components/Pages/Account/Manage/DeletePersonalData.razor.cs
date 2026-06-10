using System.ComponentModel.DataAnnotations;
using BlazorWebAppTemplate.Data;
using BlazorWebAppTemplate.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace BlazorWebAppTemplate.Components.Account.Pages.Manage;

/// <summary>
/// Delete personal data page using <c>InteractiveServer</c> render mode.
/// Permanently removes the user account after password confirmation.
/// </summary>
public partial class DeletePersonalData : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// Manages user accounts. Used for password verification and account deletion.
    /// </summary>
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    /// <summary>
    /// Provides navigation actions.
    /// </summary>
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    /// <summary>
    /// Structured logger for recording account deletion events.
    /// </summary>
    [Inject] private ILogger<DeletePersonalData> Logger { get; set; } = default!;

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
    /// Status message displayed on error.
    /// </summary>
    protected string? StatusMessage { get; set; }

    /// <summary>
    /// Controls the submit button's disabled state and loading spinner.
    /// </summary>
    protected bool IsBusy { get; set; }

    /// <summary>
    /// Whether the user has a local password and must confirm it before deletion.
    /// </summary>
    protected bool RequirePassword { get; private set; }

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
    /// Loads the current user and determines if password confirmation is required.
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

        RequirePassword = await UserManager.HasPasswordAsync(user);
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Deletes the user account on valid form submission.
    /// Navigates to logout to clear the authentication cookie.
    /// </summary>
    protected async Task OnValidSubmitAsync()
    {
        if (IsBusy || user is null) return;
        IsBusy = true;
        StatusMessage = null;

        try
        {
            if (RequirePassword && !await UserManager.CheckPasswordAsync(user, Input.Password))
            {
                StatusMessage = "Error: Incorrect password.";
                return;
            }

            var result = await UserManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                StatusMessage = "Error: Unexpected error occurred deleting user.";
                return;
            }

            var userId = await UserManager.GetUserIdAsync(user);
            Logger.LogInformation("User with ID '{UserId}' deleted themselves.", userId);

            NavigationManager.NavigateTo("Account/Logout", forceLoad: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    #region Input Model

    /// <summary>
    /// Form model for the delete confirmation.
    /// </summary>
    private sealed class InputModel
    {
        /// <summary>
        /// The user's password for confirmation. Only required when the user has a local password.
        /// </summary>
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";
    }

    #endregion
}
