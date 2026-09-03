using System.ComponentModel.DataAnnotations;
using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Application.Features.Template.Authentication;
using AspireWebAppTemplate.Application.Features.Template.AuditLog;
using AspireWebAppTemplate.Application.Features.Template.Roles;
using AspireWebAppTemplate.Application.Features.Template.Users;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Auth;

/// <summary>
/// Two-factor authentication login page. Verifies TOTP code via the API.
/// </summary>
public partial class LoginWith2fa : ComponentBase
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
    [Inject] private ILogger<LoginWith2fa> Logger { get; set; } = default!;

    #endregion

    #region Query Parameters

    /// <summary>
    /// The URL to redirect to after successful authentication.
    /// </summary>
    [SupplyParameterFromQuery]
    private string? ReturnUrl { get; set; }

    /// <summary>
    /// Whether the user selected "Remember me" on the login page.
    /// </summary>
    [SupplyParameterFromQuery]
    private bool RememberMe { get; set; }

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
    /// Error message displayed on authentication failure.
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
    /// Verifies the two-factor code via the API and navigates based on the result.
    /// </summary>
    protected async Task OnValidSubmitAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var request = new LoginWith2faRequest
            {
                Code = Input.TwoFactorCode,
                RememberMe = RememberMe,
                RememberMachine = Input.RememberMachine
            };

            var apiResult = await AuthService.LoginWith2faAsync(request);

            if (!apiResult.Succeeded || apiResult.Data is null)
            {
                ErrorMessage = apiResult.Error ?? "Unable to reach the authentication service. Please try again.";
                return;
            }

            var result = apiResult.Data;

            if (result.Succeeded)
            {
                // Navigate to PerformLogin to create the auth cookie using the single-use token
                NavigationManager.NavigateTo(
                    $"Account/PerformLogin?token={result.Token}", forceLoad: true);
            }
            else if (result.IsLockedOut)
            {
                NavigationManager.NavigateTo("Account/Lockout");
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Invalid authenticator code.";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during 2FA login.");
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
    /// Form model for the two-factor authentication page.
    /// </summary>
    private sealed class InputModel
    {
        /// <summary>
        /// The TOTP authenticator code entered by the user.
        /// </summary>
        [Required]
        [StringLength(7, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Text)]
        [Display(Name = "Authenticator code")]
        public string TwoFactorCode { get; set; } = "";

        /// <summary>
        /// Whether to suppress future 2FA prompts on this machine.
        /// </summary>
        [Display(Name = "Remember this machine")]
        public bool RememberMachine { get; set; }
    }

    #endregion
}
