using BlazorWebAppTemplate.Data;
using BlazorWebAppTemplate.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;

namespace BlazorWebAppTemplate.Components.Account.Pages.Manage;

/// <summary>
/// Two-factor authentication overview page using <c>InteractiveServer</c> render mode.
/// Displays 2FA status, recovery code count, and links to manage authenticator and recovery codes.
/// </summary>
public partial class TwoFactorAuthentication : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// Manages user accounts. Used to check 2FA status and recovery codes.
    /// </summary>
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    /// <summary>
    /// Provides navigation actions.
    /// </summary>
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

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
    /// Whether the user has an authenticator app configured.
    /// </summary>
    protected bool HasAuthenticator { get; private set; }

    /// <summary>
    /// The number of remaining recovery codes.
    /// </summary>
    protected int RecoveryCodesLeft { get; private set; }

    /// <summary>
    /// Whether 2FA is currently enabled for the user.
    /// </summary>
    protected bool Is2faEnabled { get; private set; }

    /// <summary>
    /// Status message displayed as an info alert.
    /// </summary>
    protected string? StatusMessage { get; set; }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Loads the current user's 2FA status.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateTask;
        var user = await UserManager.GetUserAsync(authState.User);
        if (user is null)
        {
            NavigationManager.NavigateTo("Account/InvalidUser", forceLoad: true);
            return;
        }

        HasAuthenticator = await UserManager.GetAuthenticatorKeyAsync(user) is not null;
        Is2faEnabled = await UserManager.GetTwoFactorEnabledAsync(user);
        RecoveryCodesLeft = await UserManager.CountRecoveryCodesAsync(user);
    }

    #endregion
}
