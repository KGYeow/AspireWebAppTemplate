using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using BlazorWebAppTemplate.Data;
using BlazorWebAppTemplate.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace BlazorWebAppTemplate.Components.Account.Pages;

/// <summary>
/// Handles the forgot password page using <c>InteractiveServer</c> render mode.
/// Allows users to request a password reset link sent to their email.
/// </summary>
/// <remarks>
/// For security, the page always navigates to the confirmation page regardless of
/// whether the email exists or is confirmed, to avoid revealing account existence.
/// </remarks>
public partial class ForgotPassword : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// Manages user accounts. Used to look up the user and generate password reset tokens.
    /// </summary>
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    /// <summary>
    /// Sends password reset emails.
    /// </summary>
    [Inject] private IEmailSender<ApplicationUser> EmailSender { get; set; } = default!;

    /// <summary>
    /// Provides navigation and URL construction.
    /// </summary>
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    /// <summary>
    /// Structured logger for recording events.
    /// </summary>
    [Inject] private ILogger<ForgotPassword> Logger { get; set; } = default!;

    #endregion

    #region State

    /// <summary>
    /// The form input model bound to the email field.
    /// </summary>
    private InputModel Input { get; set; } = new();

    /// <summary>
    /// Drives the <c>EditForm</c> validation context.
    /// </summary>
    private EditContext editContext = default!;

    /// <summary>
    /// Error message displayed in the alert banner.
    /// </summary>
    protected string? ErrorMessage { get; private set; }

    /// <summary>
    /// Controls the submit button's disabled state and loading spinner.
    /// </summary>
    protected bool IsBusy { get; private set; }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Initializes the edit context bound to <see cref="Input"/>.
    /// </summary>
    protected override void OnInitialized()
    {
        editContext = new EditContext(Input);
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Processes the form on valid submission. Looks up the user, generates a
    /// password reset token, sends the reset email, and navigates to the
    /// confirmation page.
    /// </summary>
    protected async Task OnValidSubmitAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var user = await UserManager.FindByEmailAsync(Input.Email);
            if (user is null || !await UserManager.IsEmailConfirmedAsync(user))
            {
                // Don't reveal that the user does not exist or is not confirmed
                NavigationManager.NavigateTo("Account/ForgotPasswordConfirmation", forceLoad: true);
                return;
            }

            // For more information on how to enable account confirmation and password reset please
            // visit https://go.microsoft.com/fwlink/?LinkID=532713
            var code = await UserManager.GeneratePasswordResetTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = NavigationManager.GetUriWithQueryParameters(
                NavigationManager.ToAbsoluteUri("Account/ResetPassword").AbsoluteUri,
                new Dictionary<string, object?> { ["code"] = code });

            await EmailSender.SendPasswordResetLinkAsync(user, Input.Email, HtmlEncoder.Default.Encode(callbackUrl));

            NavigationManager.NavigateTo("Account/ForgotPasswordConfirmation", forceLoad: true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error during password reset request for {Email}.", Input.Email);
            ErrorMessage = $"Unexpected error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Clears the current error message.
    /// </summary>
    protected void ClearError() => ErrorMessage = null;

    #endregion

    #region Input Model

    /// <summary>
    /// Form model bound to the forgot password form.
    /// </summary>
    private sealed class InputModel
    {
        /// <summary>
        /// The user's email address.
        /// </summary>
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;
    }

    #endregion
}
