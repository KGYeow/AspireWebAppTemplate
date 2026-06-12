using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Core.Contracts;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace AspireWebAppTemplate.Web.Components.Pages.UserManagement;

/// <summary>
/// [LDAP] Dialog for adding a user from the corporate Active Directory.
/// Looks up the user via the API, shows a preview, and creates the local user.
/// Remove this file if LDAP is not needed.
/// </summary>
public partial class AddLdapUserDialog : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// HTTP client service for user operations including LDAP lookup/creation.
    /// </summary>
    [Inject] private ApiUserService UserService { get; set; } = default!;

    #endregion

    #region Cascading Parameters

    /// <summary>
    /// The MudBlazor dialog instance.
    /// </summary>
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

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
    /// Looks up the user from the corporate directory via the API.
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
            var result = await UserService.LdapLookupAsync(Identifier.Trim());
            if (!result.Succeeded || result.Data is null)
            {
                ErrorMessage = "User not found in corporate directory.";
                return;
            }

            PreviewAttributes = result.Data;
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
    /// Creates the local user from the previewed LDAP attributes via the API.
    /// </summary>
    protected async Task AddUserAsync()
    {
        if (PreviewAttributes is null) return;

        ErrorMessage = null;
        InfoMessage = null;
        IsBusy = true;

        try
        {
            var result = await UserService.CreateLdapUserAsync(PreviewAttributes);
            if (!result.Succeeded)
            {
                ErrorMessage = result.Error ?? "Failed to create LDAP user.";
                return;
            }

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
