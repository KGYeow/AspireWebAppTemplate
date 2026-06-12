using System.ComponentModel.DataAnnotations;
using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Auth;

/// <summary>
/// Forgot password page. Sends a password reset request to the API.
/// Always navigates to the confirmation page regardless of whether the email
/// exists — this prevents revealing account existence.
/// </summary>
public partial class ForgotPassword : ComponentBase
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
    [Inject] private ILogger<ForgotPassword> Logger { get; set; } = default!;

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
    /// Error message displayed on failure.
    /// </summary>
    protected string? ErrorMessage { get; private set; }

    /// <summary>
    /// Controls the submit button's disabled state and loading spinner.
    /// </summary>
    protected bool IsBusy { get; private set; }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Initializes the edit context synchronously.
    /// </summary>
    protected override void OnInitialized()
    {
        editContext = new EditContext(Input);
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Submits the password reset request to the API and navigates to the confirmation page.
    /// </summary>
    protected async Task OnValidSubmitAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            // Call API — it handles email sending and returns success regardless
            // of whether the user exists (for security)
            await AuthService.ForgotPasswordAsync(Input.Email);

            // Always navigate to confirmation (don't reveal account existence)
            NavigationManager.NavigateTo("Account/ForgotPasswordConfirmation", forceLoad: true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during password reset request.");
            // Still navigate to confirmation for security
            NavigationManager.NavigateTo("Account/ForgotPasswordConfirmation", forceLoad: true);
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
    /// Form model for the forgot password page.
    /// </summary>
    private sealed class InputModel
    {
        /// <summary>
        /// The user's email address for password reset.
        /// </summary>
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;
    }

    #endregion
}
