using System.Text;
using BlazorWebAppTemplate.Components.Account;
using BlazorWebAppTemplate.Data;
using BlazorWebAppTemplate.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace BlazorWebAppTemplate.Components.Account.Pages;

/// <summary>
/// Displays a confirmation message after successful registration, prompting the
/// user to check their email. In development (when using the no-op email sender),
/// shows a direct confirmation link.
/// </summary>
public partial class RegisterConfirmation : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// Manages user accounts. Used to look up the user and generate confirmation tokens.
    /// </summary>
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    /// <summary>
    /// The email sender. Checked to determine if a real sender is configured
    /// or if the no-op sender is in use (development mode).
    /// </summary>
    [Inject] private IEmailSender<ApplicationUser> EmailSender { get; set; } = default!;

    /// <summary>
    /// Provides navigation and URL construction.
    /// </summary>
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

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
    /// The email address of the newly registered user.
    /// </summary>
    [SupplyParameterFromQuery]
    protected string? Email { get; set; }

    /// <summary>
    /// Optional return URL passed through from the registration page.
    /// </summary>
    [SupplyParameterFromQuery]
    private string? ReturnUrl { get; set; }

    #endregion
        
    #region State

    /// <summary>
    /// The direct email confirmation link, shown only when the no-op email sender
    /// is in use (development mode).
    /// </summary>
    protected string? EmailConfirmationLink { get; private set; }

    /// <summary>
    /// Error or status message displayed when the user cannot be found.
    /// </summary>
    protected string? StatusMessage { get; private set; }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Looks up the registered user and, if using the no-op email sender,
    /// generates a direct confirmation link for development convenience.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        if (Email is null)
        {
            RedirectManager.RedirectTo("");
            return;
        }

        var user = await UserManager.FindByEmailAsync(Email);
        if (user is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            StatusMessage = "Error finding user for the specified email.";
            return;
        }

        if (EmailSender is IdentityNoOpEmailSender)
        {
            // Once you add a real email sender, you should remove this code that lets you confirm the account
            var userId = await UserManager.GetUserIdAsync(user);
            var code = await UserManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            EmailConfirmationLink = NavigationManager.GetUriWithQueryParameters(
                NavigationManager.ToAbsoluteUri("Account/ConfirmEmail").AbsoluteUri,
                new Dictionary<string, object?>
                {
                    ["userId"] = userId,
                    ["code"] = code,
                    ["returnUrl"] = ReturnUrl
                });
        }
    }

    #endregion
}
