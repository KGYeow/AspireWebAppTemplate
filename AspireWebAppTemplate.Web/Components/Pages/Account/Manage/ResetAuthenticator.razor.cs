using BlazorWebAppTemplate.Data;
using BlazorWebAppTemplate.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace BlazorWebAppTemplate.Components.Account.Pages.Manage;

/// <summary>
/// Reset authenticator key page using <c>InteractiveServer</c> render mode.
/// Disables 2FA and resets the authenticator key, requiring reconfiguration.
/// </summary>
public partial class ResetAuthenticator : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// Manages user accounts. Used to reset the authenticator key.
    /// </summary>
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    /// <summary>
    /// Provides navigation actions.
    /// </summary>
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    /// <summary>
    /// Structured logger for recording authenticator reset events.
    /// </summary>
    [Inject] private ILogger<ResetAuthenticator> Logger { get; set; } = default!;

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
    /// Loads the current user.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateTask;
        user = await UserManager.GetUserAsync(authState.User);
        if (user is null)
        {
            NavigationManager.NavigateTo("Account/InvalidUser", forceLoad: true);
        }
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Resets the authenticator key and navigates to the EnableAuthenticator page.
    /// </summary>
    protected async Task OnSubmitAsync()
    {
        if (IsBusy || user is null) return;
        IsBusy = true;

        try
        {
            await UserManager.SetTwoFactorEnabledAsync(user, false);
            await UserManager.ResetAuthenticatorKeyAsync(user);
            var userId = await UserManager.GetUserIdAsync(user);
            Logger.LogInformation("User with ID '{UserId}' has reset their authentication app key.", userId);

            NavigationManager.NavigateTo("Account/Manage/EnableAuthenticator", forceLoad: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion
}
