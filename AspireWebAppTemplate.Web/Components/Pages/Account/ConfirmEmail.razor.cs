using System.Text;
using BlazorWebAppTemplate.Components.Account;
using BlazorWebAppTemplate.Data;
using BlazorWebAppTemplate.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace BlazorWebAppTemplate.Components.Account.Pages;

/// <summary>
/// Handles email confirmation when the user clicks the confirmation link.
/// Decodes the token, confirms the email via <see cref="UserManager{TUser}"/>,
/// and displays a success or error state.
/// </summary>
public partial class ConfirmEmail : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// Manages user accounts. Used to look up the user and confirm the email.
    /// </summary>
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    /// <summary>
    /// Identity-aware redirect manager.
    /// </summary>
    [Inject] private IdentityRedirectManager RedirectManager { get; set; } = default!;

    #endregion

    #region Cascading Parameters

    /// <summary>
    /// The current HTTP context, used to set the response status code on error.
    /// </summary>
    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    #endregion

    #region Query Parameters

    /// <summary>
    /// The user ID from the confirmation link.
    /// </summary>
    [SupplyParameterFromQuery]
    private string? UserId { get; set; }

    /// <summary>
    /// The Base64Url-encoded email confirmation token from the confirmation link.
    /// </summary>
    [SupplyParameterFromQuery]
    private string? Code { get; set; }

    #endregion

    #region State

    /// <summary>
    /// Indicates whether the email confirmation succeeded.
    /// </summary>
    protected bool IsSuccess { get; private set; }

    /// <summary>
    /// Status message displayed on error.
    /// </summary>
    protected string? StatusMessage { get; private set; }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Validates the query parameters, looks up the user, decodes the token,
    /// and confirms the email.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        if (UserId is null || Code is null)
        {
            RedirectManager.RedirectTo("");
            return;
        }

        var user = await UserManager.FindByIdAsync(UserId);
        if (user is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            StatusMessage = $"Error loading user with ID {UserId}.";
            return;
        }

        var code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(Code));
        var result = await UserManager.ConfirmEmailAsync(user, code);

        if (result.Succeeded)
        {
            IsSuccess = true;
        }
        else
        {
            StatusMessage = "Error confirming your email. The link may have expired or already been used.";
        }
    }

    #endregion
}
