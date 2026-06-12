using System.ComponentModel.DataAnnotations;
using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Core.Contracts;
using AspireWebAppTemplate.Core.Contracts.Auth;
using AspireWebAppTemplate.Core.Contracts.AuditLog;
using AspireWebAppTemplate.Core.Contracts.Roles;
using AspireWebAppTemplate.Core.Contracts.Users;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Auth;

/// <summary>
/// Handles the login page. Delegates credential validation entirely to the API
/// via <see cref="ApiAuthService"/>. The API handles LDAP fallback, audit logging,
/// and token generation internally.
/// </summary>
public partial class Login : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// Provides authentication operations via the API.
    /// </summary>
    [Inject] private ApiAuthService AuthService { get; set; } = default!;

    /// <summary>
    /// Structured logger for recording events.
    /// </summary>
    [Inject] private ILogger<Login> Logger { get; set; } = default!;

    /// <summary>
    /// Provides navigation actions.
    /// </summary>
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    #endregion

    #region Query Parameters

    /// <summary>
    /// The URL to redirect to after successful login.
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
    /// Error message displayed on login failure.
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
    /// The placeholder text for the identifier field.
    /// </summary>
    // The placeholder always shows "Email or NTID" since the API handles LDAP internally
    protected string IdentifierPlaceholder => "Email or NTID";

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
    /// Validates credentials via the API and navigates based on the result.
    /// </summary>
    protected async Task LoginUser()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var request = new LoginRequest
            {
                Email = Input.Email,
                Password = Input.Password,
                RememberMe = Input.RememberMe,
                ReturnUrl = ReturnUrl
            };

            var apiResult = await AuthService.LoginAsync(request);

            if (!apiResult.Succeeded || apiResult.Data is null)
            {
                ErrorMessage = apiResult.Error ?? "Unable to reach the authentication service. Please try again.";
                return;
            }

            var result = apiResult.Data;

            if (result.Succeeded)
            {
                NavigationManager.NavigateTo($"Account/PerformLogin?token={result.Token}", forceLoad: true);
            }
            else if (result.RequiresTwoFactor)
            {
                NavigationManager.NavigateTo(
                    $"Account/LoginWith2fa?returnUrl={ReturnUrl}&rememberMe={Input.RememberMe}",
                    forceLoad: true);
            }
            else if (result.IsLockedOut)
            {
                NavigationManager.NavigateTo("Account/Lockout");
            }
            else if (result.IsDeactivated)
            {
                NavigationManager.NavigateTo("Account/AccessDenied?reason=inactive", forceLoad: true);
            }
            else
            {
                ErrorMessage = result.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error during login for {Identifier}.", Input.Email);
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
    /// Clears the current error message.
    /// </summary>
    protected void ClearError() => ErrorMessage = null;

    #endregion

    #region Input Model

    /// <summary>
    /// Form model for the login page with DataAnnotations validation.
    /// </summary>
    private sealed class InputModel
    {
        /// <summary>
        /// The user's email address or NTID.
        /// </summary>
        [Required]
        [Display(Name = "Email or NTID")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// The user's password.
        /// </summary>
        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Whether to persist the authentication cookie.
        /// </summary>
        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
    }

    #endregion
}
