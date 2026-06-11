using System.ComponentModel.DataAnnotations;
using AspireWebAppTemplate.Core.Contracts;
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

    [Inject] private ApiAuthService AuthService { get; set; } = default!;
    [Inject] private ILogger<Register> Logger { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    #endregion

    #region Query Parameters

    [SupplyParameterFromQuery]
    private string? ReturnUrl { get; set; }

    #endregion

    #region State

    private InputModel Input { get; set; } = new();
    private EditContext editContext = default!;
    protected string? ErrorMessage { get; private set; }
    protected bool IsBusy { get; private set; }
    protected bool IsPasswordVisible { get; private set; }
    protected bool IsConfirmPasswordVisible { get; private set; }

    #endregion

    #region Lifecycle

    protected override void OnInitialized()
    {
        editContext = new EditContext(Input);
    }

    #endregion

    #region Event Handlers

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

            var result = await AuthService.RegisterAsync(request);

            if (result is null)
            {
                ErrorMessage = "Unable to reach the registration service. Please try again.";
                return;
            }

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

    protected void TogglePasswordVisibility() => IsPasswordVisible = !IsPasswordVisible;
    protected void ToggleConfirmPasswordVisibility() => IsConfirmPasswordVisible = !IsConfirmPasswordVisible;
    protected void ClearError() => ErrorMessage = null;

    #endregion

    #region Input Model

    private sealed class InputModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    #endregion
}
