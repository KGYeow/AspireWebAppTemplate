using BlazorWebAppTemplate.Data;
using BlazorWebAppTemplate.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace BlazorWebAppTemplate.Components.Account.Pages.Manage;

/// <summary>
/// Disable two-factor authentication page using <c>InteractiveServer</c> render mode.
/// </summary>
public partial class Disable2fa : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// Manages user accounts. Used to disable 2FA.
    /// </summary>
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    /// <summary>
    /// Provides navigation actions.
    /// </summary>
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    /// <summary>
    /// Structured logger for recording 2FA events.
    /// </summary>
    [Inject] private ILogger<Disable2fa> Logger { get; set; } = default!;

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
    /// Status message displayed on error.
    /// </summary>
    protected string? StatusMessage { get; set; }

    /// <summary>
    /// Controls the button's disabled state and loading spinner.
    /// </summary>
    protected bool IsBusy { get; set; }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Loads the current user and validates that 2FA is currently enabled.
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

        if (!await UserManager.GetTwoFactorEnabledAsync(user))
        {
            StatusMessage = "Error: Cannot disable 2FA as it's not currently enabled.";
        }
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Disables 2FA for the current user and navigates to the 2FA overview page.
    /// </summary>
    protected async Task OnSubmitAsync()
    {
        if (IsBusy || user is null) return;
        IsBusy = true;

        try
        {
            var result = await UserManager.SetTwoFactorEnabledAsync(user, false);
            if (!result.Succeeded)
            {
                StatusMessage = "Error: Unexpected error occurred disabling 2FA.";
                return;
            }

            var userId = await UserManager.GetUserIdAsync(user);
            Logger.LogInformation("User with ID '{UserId}' has disabled 2fa.", userId);
            NavigationManager.NavigateTo("Account/Manage/TwoFactorAuthentication", forceLoad: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion
}
