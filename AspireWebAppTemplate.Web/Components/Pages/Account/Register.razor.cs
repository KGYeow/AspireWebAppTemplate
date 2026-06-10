using System.ComponentModel.DataAnnotations;
using BlazorWebAppTemplate.Abstractions;
using BlazorWebAppTemplate.Contracts;
using BlazorWebAppTemplate.Components.Account;
using BlazorWebAppTemplate.Data;
using BlazorWebAppTemplate.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace BlazorWebAppTemplate.Components.Account.Pages;

/// <summary>
/// Handles the registration page using <c>InteractiveServer</c> render mode,
/// enabling full Blazor interactivity (password visibility toggles, loading state,
/// inline error display). Delegates registration logic to <see cref="IRegisterService"/>.
/// </summary>
public partial class Register : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// The registration service responsible for user creation, role assignment, and email confirmation.
    /// </summary>
    [Inject] private IRegisterService RegisterService { get; set; } = default!;

    /// <summary>
    /// Handles sign-in operations. Used to sign in the user after registration
    /// when email confirmation is not required.
    /// </summary>
    [Inject] private SignInManager<ApplicationUser> SignInManager { get; set; } = default!;

    /// <summary>
    /// Structured logger for recording registration events.
    /// </summary>
    [Inject] private ILogger<Register> Logger { get; set; } = default!;

    /// <summary>
    /// Provides navigation actions used to redirect after registration.
    /// </summary>
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    /// <summary>
    /// Identity-aware redirect manager for post-registration navigation.
    /// </summary>
    [Inject] private IdentityRedirectManager RedirectManager { get; set; } = default!;

    #endregion

    #region Query Parameters

    /// <summary>
    /// Optional return URL supplied via query string.
    /// The user is redirected here after successful registration and sign-in.
    /// </summary>
    [SupplyParameterFromQuery]
    private string? ReturnUrl { get; set; }

    #endregion

    #region State

    /// <summary>
    /// The form input model bound to the registration form fields.
    /// </summary>
    private InputModel Input { get; set; } = new();

    /// <summary>
    /// Drives the <c>EditForm</c> validation context.
    /// </summary>
    private EditContext editContext = default!;

    /// <summary>
    /// Error message displayed in the alert banner when registration fails.
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
    /// Initializes the edit context bound to <see cref="Input"/>.
    /// </summary>
    protected override void OnInitialized()
    {
        editContext = new EditContext(Input);
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Processes the registration form on valid submission.
    /// Delegates to <see cref="IRegisterService"/> and navigates based on the result.
    /// </summary>
    protected async Task RegisterUser()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var confirmEmailBaseUri = NavigationManager.ToAbsoluteUri("Account/ConfirmEmail").AbsoluteUri;

            var result = await RegisterService.RegisterUserAsync(
                Input.Email,
                Input.Password,
                confirmEmailBaseUri,
                ReturnUrl);

            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            if (result.RequiresEmailConfirmation)
            {
                RedirectManager.RedirectTo(
                    "Account/RegisterConfirmation",
                    new() { ["email"] = result.Email, ["returnUrl"] = ReturnUrl });
            }
            else
            {
                // Sign in directly when email confirmation is not required
                var user = await SignInManager.UserManager.FindByEmailAsync(Input.Email);
                if (user is not null)
                {
                    await SignInManager.SignInAsync(user, isPersistent: false);
                }
                RedirectManager.RedirectTo(ReturnUrl);
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
    /// Form model bound to the registration form fields.
    /// </summary>
    private sealed class InputModel
    {
        /// <summary>
        /// The user's email address used as the registration identifier.
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
        /// Confirmation of the user's chosen password. Must match <see cref="Password"/>.
        /// </summary>
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    #endregion
}
