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
/// Handles the registration page. Delegates registration logic to the API
/// via <see cref="ApiAuthService"/>.
/// </summary>
public partial class Register : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// Provides authentication and registration operations via the API.
    /// </summary>
    [Inject] private ApiAuthService AuthService { get; set; } = default!;

    /// <summary>
    /// Structured logger for recording events.
    /// </summary>
    [Inject] private ILogger<Register> Logger { get; set; } = default!;

    /// <summary>
    /// Provides navigation actions.
    /// </summary>
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    #endregion

    #region Query Parameters

    /// <summary>
    /// The URL to redirect to after successful registration.
    /// </summary>
    [SupplyParameterFromQuery]
    private string? ReturnUrl { get; set; }

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
    /// Error message displayed on registration failure.
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
    /// Initializes the edit context synchronously.
    /// </summary>
    protected override void OnInitialized()
    {
        editContext = new EditContext(Input);
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Submits the registration request to the API and navigates based on the result.
    /// </summary>
    protected async Task RegisterUser()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var request = new LoginRequest
            {
                Email = Input.Email,
                Password = Input.Password
            };

            var apiResult = await AuthService.RegisterAsync(request);

            if (!apiResult.Succeeded || apiResult.Data is null)
            {
                ErrorMessage = apiResult.Error ?? "Unable to reach the registration service. Please try again.";
                return;
            }

            var result = apiResult.Data;

            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            if (result.RequiresEmailConfirmation)
            {
                NavigationManager.NavigateTo(
                    $"Account/RegisterConfirmation?email={Uri.EscapeDataString(Input.Email)}&returnUrl={ReturnUrl}",
                    forceLoad: true);
            }
            else if (!string.IsNullOrEmpty(result.Token))
            {
                // Auto-sign-in: navigate to PerformLogin with the token
                NavigationManager.NavigateTo(
                    $"Account/PerformLogin?token={result.Token}", forceLoad: true);
            }
            else
            {
                // Fallback: redirect to login
                NavigationManager.NavigateTo("Account/Login", forceLoad: true);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error during registration.");
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
    /// Form model for the registration page with DataAnnotations validation.
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
        /// The user's chosen password.
        /// </summary>
        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Confirmation of the user's chosen password.
        /// </summary>
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    #endregion
}
