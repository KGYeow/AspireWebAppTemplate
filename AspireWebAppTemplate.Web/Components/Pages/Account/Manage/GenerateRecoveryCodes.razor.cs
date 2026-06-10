using BlazorWebAppTemplate.Data;
using BlazorWebAppTemplate.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace BlazorWebAppTemplate.Components.Account.Pages.Manage;

/// <summary>
/// Generate new 2FA recovery codes page using <c>InteractiveServer</c> render mode.
/// </summary>
public partial class GenerateRecoveryCodes : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// Manages user accounts. Used to generate recovery codes.
    /// </summary>
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    /// <summary>
    /// Provides navigation actions.
    /// </summary>
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    /// <summary>
    /// Structured logger for recording recovery code generation events.
    /// </summary>
    [Inject] private ILogger<GenerateRecoveryCodes> Logger { get; set; } = default!;

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
    /// The generated recovery codes, displayed after generation.
    /// </summary>
    protected string[]? RecoveryCodes { get; private set; }

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
    /// Loads the current user and validates that 2FA is enabled.
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
            StatusMessage = "Error: Cannot generate recovery codes because 2FA is not enabled.";
        }
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Generates new recovery codes for the current user.
    /// </summary>
    protected async Task OnSubmitAsync()
    {
        if (IsBusy || user is null) return;
        IsBusy = true;

        try
        {
            var userId = await UserManager.GetUserIdAsync(user);
            var codes = await UserManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
            RecoveryCodes = codes?.ToArray();

            Logger.LogInformation("User with ID '{UserId}' has generated new 2FA recovery codes.", userId);
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion
}
