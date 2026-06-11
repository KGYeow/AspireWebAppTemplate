using System.ComponentModel.DataAnnotations;
using AspireWebAppTemplate.Core.Contracts;
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

    [Inject] private ApiAuthService AuthService { get; set; } = default!;
    [Inject] private ILogger<Login> Logger { get; set; } = default!;
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

    // The placeholder always shows "Email or NTID" since the API handles LDAP internally
    protected string IdentifierPlaceholder => "Email or NTID";

    #endregion

    #region Lifecycle

    protected override void OnInitialized()
    {
        editContext = new EditContext(Input);
    }

    #endregion

    #region Event Handlers

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
                RememberMe = Input.RememberMe
            };

            var result = await AuthService.LoginAsync(request);

            if (result is null)
            {
                ErrorMessage = "Unable to reach the authentication service. Please try again.";
                return;
            }

            if (result.Succeeded)
            {
                NavigationManager.NavigateTo(
                    $"Account/PerformLogin?token={result.Token}", forceLoad: true);
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

    protected void TogglePasswordVisibility() => IsPasswordVisible = !IsPasswordVisible;
    protected void ClearError() => ErrorMessage = null;

    #endregion

    #region Input Model

    private sealed class InputModel
    {
        [Required]
        [Display(Name = "Email or NTID")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
    }

    #endregion
}
