using System.ComponentModel.DataAnnotations;
using System.Text;
using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Auth;

/// <summary>
/// Reset password page. Allows users to set a new password using a reset token
/// received via email. Delegates password reset to the API via <see cref="ApiAuthService"/>.
/// </summary>
public partial class ResetPassword : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// Provides authentication operations via the API.
    /// </summary>
    [Inject] private ApiAuthService AuthService { get; set; } = default!;

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
    /// The Base64Url-encoded reset token from the email link.
    /// </summary>
    [SupplyParameterFromQuery]
    private string? Code { get; set; }

    #endregion

    #region State

    /// <summary>
    /// The form input model.
    /// </summary>
    private InputModel Input { get; set; } = new();

    /// <summary>
    /// Drives the <c>EditForm</c> validation context.
    /// </summary>
    private EditContext editContext = default!;

    /// <summary>
    /// The decoded reset token extracted from the query string.
    /// </summary>
    private string decodedCode = string.Empty;

    /// <summary>
    /// Error message displayed on failure.
    /// </summary>
    protected string? ErrorMessage { get; private set; }

    /// <summary>
    /// Controls the submit button's disabled state and loading spinner.
    /// </summary>
    protected bool IsBusy { get; private set; }

    /// <summary>
    /// Controls the password field visibility toggle.
    /// </summary>
    protected bool IsPasswordVisible { get; private set; }

    /// <summary>
    /// Controls the confirm password field visibility toggle.
    /// </summary>
    protected bool IsConfirmPasswordVisible { get; private set; }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Decodes the reset token and initializes the edit context.
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
    /// Submits the new password to the API for reset.
    /// </summary>
    protected async Task OnValidSubmitAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var result = await AuthService.ResetPasswordAsync(Input.Email, decodedCode, Input.Password);
            if (result.Succeeded)
            {
                NavigationManager.NavigateTo("Account/ResetPasswordConfirmation", forceLoad: true);
            }
            else
            {
                ErrorMessage = result.Error ?? "Failed to reset password. The link may have expired.";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during password reset.");
            ErrorMessage = $"Unexpected error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Toggles the password field between visible and masked.
    /// </summary>
    protected void TogglePasswordVisibility() => IsPasswordVisible = !IsPasswordVisible;

    /// <summary>
    /// Toggles the confirm password field between visible and masked.
    /// </summary>
    protected void ToggleConfirmPasswordVisibility() => IsConfirmPasswordVisible = !IsConfirmPasswordVisible;

    /// <summary>
    /// Clears the current error message.
    /// </summary>
    protected void ClearError() => ErrorMessage = null;

    #endregion

    #region Input Model

    /// <summary>
    /// Form model for the reset password page.
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
        /// The new password.
        /// </summary>
        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Confirmation of the new password.
        /// </summary>
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    #endregion
}
