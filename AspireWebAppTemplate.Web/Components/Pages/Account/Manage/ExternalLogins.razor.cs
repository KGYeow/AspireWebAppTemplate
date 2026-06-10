using BlazorWebAppTemplate.Data;
using BlazorWebAppTemplate.Data.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;

namespace BlazorWebAppTemplate.Components.Account.Pages.Manage;

/// <summary>
/// Manage external logins page using <c>InteractiveServer</c> render mode.
/// Lists current external logins and allows removing them or linking new ones.
/// </summary>
public partial class ExternalLogins : ComponentBase
{
    #region Constants

    /// <summary>
    /// The action identifier used for the link login callback.
    /// </summary>
    public const string LinkLoginCallbackAction = "LinkLoginCallback";

    #endregion

    #region Injected Services

    /// <summary>
    /// Manages user accounts. Used for login management operations.
    /// </summary>
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    /// <summary>
    /// Handles sign-in operations. Used to get external authentication schemes.
    /// </summary>
    [Inject] private SignInManager<ApplicationUser> SignInManager { get; set; } = default!;

    /// <summary>
    /// Provides access to the underlying user store for password hash checks.
    /// </summary>
    [Inject] private IUserStore<ApplicationUser> UserStore { get; set; } = default!;

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
    /// The current user.
    /// </summary>
    private ApplicationUser? user;

    /// <summary>
    /// The user's currently linked external logins.
    /// </summary>
    protected IList<UserLoginInfo>? CurrentLogins { get; private set; }

    /// <summary>
    /// Available external login providers not yet linked to the user.
    /// </summary>
    protected IList<AuthenticationScheme>? OtherLogins { get; private set; }

    /// <summary>
    /// Whether the remove button should be shown (user has a password or multiple logins).
    /// </summary>
    protected bool ShowRemoveButton { get; private set; }

    /// <summary>
    /// Status message displayed after an action.
    /// </summary>
    protected string? StatusMessage { get; set; }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Loads the current user's external logins and available providers.
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

        CurrentLogins = await UserManager.GetLoginsAsync(user);
        OtherLogins = (await SignInManager.GetExternalAuthenticationSchemesAsync())
            .Where(auth => CurrentLogins.All(ul => auth.Name != ul.LoginProvider))
            .ToList();

        string? passwordHash = null;
        if (UserStore is IUserPasswordStore<ApplicationUser> userPasswordStore)
        {
            passwordHash = await userPasswordStore.GetPasswordHashAsync(user, CancellationToken.None);
        }

        ShowRemoveButton = passwordHash is not null || CurrentLogins.Count > 1;
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Removes an external login from the user's account.
    /// </summary>
    /// <param name="loginProvider">The external login provider name.</param>
    /// <param name="providerKey">The provider-specific key for the login.</param>
    protected async Task RemoveLoginAsync(string loginProvider, string providerKey)
    {
        if (user is null) return;

        var result = await UserManager.RemoveLoginAsync(user, loginProvider, providerKey);
        if (!result.Succeeded)
        {
            StatusMessage = "Error: The external login was not removed.";
            return;
        }

        StatusMessage = "The external login was removed.";
        CurrentLogins = await UserManager.GetLoginsAsync(user);
    }

    #endregion
}
