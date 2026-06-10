using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using BlazorWebAppTemplate.Data;
using BlazorWebAppTemplate.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace BlazorWebAppTemplate.Components.Account.Pages.Manage;

/// <summary>
/// Manage email page using <c>InteractiveServer</c> render mode.
/// Allows changing the email address and sending a verification email.
/// </summary>
public partial class Email : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// Manages user accounts. Used for email operations and token generation.
    /// </summary>
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    /// <summary>
    /// Sends confirmation and change-email emails.
    /// </summary>
    [Inject] private IEmailSender<ApplicationUser> EmailSender { get; set; } = default!;

    /// <summary>
    /// Provides navigation and URL construction.
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
    /// The form input model.
    /// </summary>
    private InputModel Input { get; set; } = new();

    /// <summary>
    /// Drives the <c>EditForm</c> validation context.
    /// </summary>
    private EditContext editContext = default!;

    /// <summary>
    /// The user's current email address.
    /// </summary>
    protected string? CurrentEmail { get; private set; }

    /// <summary>
    /// Whether the user's current email is confirmed.
    /// </summary>
    protected bool IsEmailConfirmed { get; private set; }

    /// <summary>
    /// Status message displayed after an action.
    /// </summary>
    protected string? StatusMessage { get; set; }

    /// <summary>
    /// Controls the button disabled states and loading spinner.
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
    /// Loads the current user's email data.
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

        CurrentEmail = await UserManager.GetEmailAsync(user);
        IsEmailConfirmed = await UserManager.IsEmailConfirmedAsync(user);
        Input.NewEmail ??= CurrentEmail;
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Sends a change-email confirmation link on valid form submission.
    /// </summary>
    protected async Task OnValidSubmitAsync()
    {
        if (IsBusy || user is null) return;
        IsBusy = true;
        StatusMessage = null;

        try
        {
            if (Input.NewEmail is null || Input.NewEmail == CurrentEmail)
            {
                StatusMessage = "Your email is unchanged.";
                return;
            }

            var userId = await UserManager.GetUserIdAsync(user);
            var code = await UserManager.GenerateChangeEmailTokenAsync(user, Input.NewEmail);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = NavigationManager.GetUriWithQueryParameters(
                NavigationManager.ToAbsoluteUri("Account/ConfirmEmailChange").AbsoluteUri,
                new Dictionary<string, object?> { ["userId"] = userId, ["email"] = Input.NewEmail, ["code"] = code });

            await EmailSender.SendConfirmationLinkAsync(user, Input.NewEmail, HtmlEncoder.Default.Encode(callbackUrl));
            StatusMessage = "Confirmation link to change email sent. Please check your email.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Sends a verification email for the current email address.
    /// </summary>
    protected async Task OnSendEmailVerificationAsync()
    {
        if (IsBusy || user is null || CurrentEmail is null) return;
        IsBusy = true;
        StatusMessage = null;

        try
        {
            var userId = await UserManager.GetUserIdAsync(user);
            var code = await UserManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = NavigationManager.GetUriWithQueryParameters(
                NavigationManager.ToAbsoluteUri("Account/ConfirmEmail").AbsoluteUri,
                new Dictionary<string, object?> { ["userId"] = userId, ["code"] = code });

            await EmailSender.SendConfirmationLinkAsync(user, CurrentEmail, HtmlEncoder.Default.Encode(callbackUrl));
            StatusMessage = "Verification email sent. Please check your email.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    #region Input Model

    /// <summary>
    /// Form model for the change email form.
    /// </summary>
    private sealed class InputModel
    {
        /// <summary>
        /// The new email address.
        /// </summary>
        [Required]
        [EmailAddress]
        [Display(Name = "New email")]
        public string? NewEmail { get; set; }
    }

    #endregion
}
