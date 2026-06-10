using System.ComponentModel.DataAnnotations;
using System.Text;
using BlazorWebAppTemplate.Data;
using BlazorWebAppTemplate.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace BlazorWebAppTemplate.Components.Account.Pages;

/// <summary>
/// Handles the password reset page using <c>InteractiveServer</c> render mode.
/// The user arrives here from the password reset email link with a token in the
/// query string.
/// </summary>
public partial class ResetPassword : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// Manages user accounts. Used to find the user and reset the password.
    /// </summary>
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    /// <summary>
    /// Provides navigation actions.
    /// </summary>
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    /// <summary>
    /// Structured logger for recording events.
    /// </summary>
    [Inject] private ILogger<ResetPassword> Logger { get; set; } = default!;

    #endregion

    #region Query Parameters

    /// <summary>
    /// The Base64Url-encoded password reset token from the email link.
    /// </summary>
    [SupplyParameterFromQuery]
    private string? Code { get; set; }

    #endregion

    #region State

    /// <summary>
    /// The form input model bound to the reset password form fields.
    /// </summary>
    private InputModel Input { get; set; } = new();

    /// <summary>
    /// Drives the <c>EditForm</c> validation context.
    /// </summary>
    private EditContext editContext = default!;

    /// <summary>
    /// The decoded password reset token.
    /// </summary>
    private string decodedCode = string.Empty;

    /// <summary>
    /// Error message displayed in the alert banner.
    /// </summary>
    protected string? ErrorMessage { get; private set; }

    /// <summary>
    /// Controls the submit button's disabled state and loading spinner.
    /// </summary>
    protected bool IsBusy { get; private set; }

    /// <summary>
    /// Toggles the password field between masked and plain-text display.
    /// </summary>
    protected bool IsPasswordVisible { get; private set; }

    /// <summary>
    /// Toggles the confirm password field between masked and plain-text display.
    /// </summary>
    protected bool IsConfirmPasswordVisible { get; private set; }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Decodes the reset token from the query string. Redirects to the invalid
    /// password reset page if the code is missing.
    /// </summary>
    protected override void OnInitialized()
    {
        if (Code is null)
        {
            NavigationManager.NavigateTo("Account/InvalidPasswordReset", forceLoad: true);
            return;
        }

        decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(Code));
        editContext = new EditContext(Input);
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Processes the form on valid submission. Finds the user, resets the password,
    /// and navigates to the confirmation page.
    /// </summary>
    protected async Task OnValidSubmitAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var user = await UserManager.FindByEmailAsync(Input.Email);
            if (user is null)
            {
                // Don't reveal that the user does not exist
                NavigationManager.NavigateTo("Account/ResetPasswordConfirmation", forceLoad: true);
                return;
            }

            var result = await UserManager.ResetPasswordAsync(user, decodedCode, Input.Password);
            if (result.Succeeded)
            {
                NavigationManager.NavigateTo("Account/ResetPasswordConfirmation", forceLoad: true);
                return;
            }

            ErrorMessage = string.Join(" ", result.Errors.Select(e => e.Description));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error during password reset.");
            ErrorMessage = $"Unexpected error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Toggles the password field visibility.
    /// </summary>
    protected void TogglePasswordVisibility() => IsPasswordVisible = !IsPasswordVisible;

    /// <summary>
    /// Toggles the confirm password field visibility.
    /// </summary>
    protected void ToggleConfirmPasswordVisibility() => IsConfirmPasswordVisible = !IsConfirmPasswordVisible;

    /// <summary>
    /// Clears the current error message.
    /// </summary>
    protected void ClearError() => ErrorMessage = null;

    #endregion

    #region Input Model

    /// <summary>
    /// Form model bound to the reset password form.
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

        /// <summary>
        /// The user's new password.
        /// </summary>
        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Confirmation of the new password. Must match <see cref="Password"/>.
        /// </summary>
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    #endregion
}
