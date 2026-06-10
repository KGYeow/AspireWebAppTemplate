using System.Buffers.Text;
using System.ComponentModel.DataAnnotations;
using BlazorWebAppTemplate.Data;
using BlazorWebAppTemplate.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Identity;

namespace BlazorWebAppTemplate.Components.Account.Pages.Manage;

/// <summary>
/// Rename passkey page using <c>InteractiveServer</c> render mode.
/// Allows the user to set or change the display name of a passkey.
/// </summary>
public partial class RenamePasskey : ComponentBase
{
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

    #region Route Parameters

    /// <summary>
    /// The Base64Url-encoded credential ID of the passkey to rename.
    /// </summary>
    [Parameter]
    public string? Id { get; set; }

    #endregion

    #region State

    /// <summary>
    /// The current user.
    /// </summary>
    private ApplicationUser? user;

    /// <summary>
    /// The passkey being renamed.
    /// </summary>
    private UserPasskeyInfo? passkey;

    /// <summary>
    /// The form input model.
    /// </summary>
    private InputModel Input { get; set; } = new();

    /// <summary>
    /// Drives the <c>EditForm</c> validation context.
    /// </summary>
    private EditContext editContext = default!;

    /// <summary>
    /// The page header title, dynamically set based on the passkey's current name.
    /// </summary>
    protected string HeaderTitle { get; private set; } = "Rename Passkey";

    /// <summary>
    /// Status message displayed on error.
    /// </summary>
    protected string? StatusMessage { get; set; }

    /// <summary>
    /// Controls the submit button's disabled state and loading spinner.
    /// </summary>
    protected bool IsBusy { get; set; }

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
    /// Loads the current user and the passkey to rename.
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

        byte[] credentialId;
        try
        {
            credentialId = Base64Url.DecodeFromChars(Id);
        }
        catch (FormatException)
        {
            NavigationManager.NavigateTo("Account/Manage/Passkeys", forceLoad: true);
            return;
        }

        passkey = await UserManager.GetPasskeyAsync(user, credentialId);
        if (passkey is null)
        {
            NavigationManager.NavigateTo("Account/Manage/Passkeys", forceLoad: true);
            return;
        }

        HeaderTitle = passkey.Name is not null
            ? $"Enter a new name for your \"{passkey.Name}\" passkey"
            : "Enter a name for your passkey";
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Renames the passkey on valid form submission.
    /// </summary>
    protected async Task OnRenameAsync()
    {
        if (IsBusy || user is null || passkey is null) return;
        IsBusy = true;

        try
        {
            passkey.Name = Input.Name;
            var result = await UserManager.AddOrUpdatePasskeyAsync(user, passkey);
            if (!result.Succeeded)
            {
                StatusMessage = "Error: The passkey could not be updated.";
                return;
            }

            NavigationManager.NavigateTo("Account/Manage/Passkeys", forceLoad: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    #region Input Model

    /// <summary>
    /// Form model for the passkey rename form.
    /// </summary>
    private sealed class InputModel
    {
        /// <summary>
        /// The new display name for the passkey.
        /// </summary>
        [Required]
        [StringLength(200, ErrorMessage = "Passkey names must be no longer than {1} characters.")]
        public string Name { get; set; } = "";
    }

    #endregion
}
