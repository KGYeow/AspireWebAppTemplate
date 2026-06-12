using System.ComponentModel.DataAnnotations;
using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Core.Contracts;
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

    [Inject] private ApiAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<LoginWith2fa> Logger { get; set; } = default!;

    #endregion

    #region Query Parameters

    [SupplyParameterFromQuery]
    private string? ReturnUrl { get; set; }

    [SupplyParameterFromQuery]
    private bool RememberMe { get; set; }

    #endregion

    #region State

    private InputModel Input { get; set; } = new();
    private EditContext editContext = default!;
    protected string? ErrorMessage { get; private set; }
    protected bool IsBusy { get; private set; }

    #endregion

    #region Lifecycle

    protected override void OnInitialized()
    {
        editContext = new EditContext(Input);
    }

    #endregion

    #region Event Handlers

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
                var returnUrl = ReturnUrl ?? "/";
                NavigationManager.NavigateTo(returnUrl, forceLoad: true);
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

    protected void ClearError() => ErrorMessage = null;

    #endregion

    #region Input Model

    private sealed class InputModel
    {
        [Required]
        [StringLength(7, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Text)]
        [Display(Name = "Authenticator code")]
        public string TwoFactorCode { get; set; } = "";

        [Display(Name = "Remember this machine")]
        public bool RememberMachine { get; set; }
    }

    #endregion
}
