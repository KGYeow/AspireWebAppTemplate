using System.Buffers.Text;
using BlazorWebAppTemplate.Data;
using BlazorWebAppTemplate.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;

namespace BlazorWebAppTemplate.Components.Account.Pages.Manage;

/// <summary>
/// Manage passkeys page using <c>InteractiveServer</c> render mode.
/// Lists current passkeys with rename and delete actions.
/// </summary>
/// <remarks>
/// Adding passkeys requires the <c>PasskeySubmit</c> component which uses JS interop
/// and antiforgery tokens that only work in Static SSR. The add flow navigates to a
/// full page reload to handle this.
/// </remarks>
public partial class Passkeys : ComponentBase
{
    #region Constants

    /// <summary>
    /// Maximum number of passkeys allowed per user.
    /// </summary>
    internal const int MaxPasskeyCount = 100;

    #endregion

    #region Injected Services

    /// <summary>
    /// Manages user accounts. Used for passkey operations.
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
    /// The current user.
    /// </summary>
    private ApplicationUser? user;

    /// <summary>
    /// The user's current passkeys.
    /// </summary>
    protected IList<UserPasskeyInfo>? CurrentPasskeys { get; private set; }

    /// <summary>
    /// Status message displayed after an action.
    /// </summary>
    protected string? StatusMessage { get; set; }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Loads the current user and their passkeys.
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

        CurrentPasskeys = await UserManager.GetPasskeysAsync(user);
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Deletes a passkey by its Base64Url-encoded credential ID.
    /// </summary>
    /// <param name="credentialIdBase64">The Base64Url-encoded credential ID.</param>
    protected async Task DeletePasskeyAsync(string credentialIdBase64)
    {
        if (user is null) return;

        byte[] credentialId;
        try
        {
            credentialId = Base64Url.DecodeFromChars(credentialIdBase64);
        }
        catch (FormatException)
        {
            StatusMessage = "Error: The specified passkey ID had an invalid format.";
            return;
        }

        var result = await UserManager.RemovePasskeyAsync(user, credentialId);
        if (!result.Succeeded)
        {
            StatusMessage = "Error: The passkey could not be deleted.";
            return;
        }

        StatusMessage = "Passkey deleted successfully.";
        CurrentPasskeys = await UserManager.GetPasskeysAsync(user);
    }

    #endregion
}
