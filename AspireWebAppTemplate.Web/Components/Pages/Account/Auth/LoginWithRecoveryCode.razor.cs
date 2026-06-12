using System.ComponentModel.DataAnnotations;
using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Auth;

/// <summary>
/// Recovery code login page. Verifies a recovery code via the API.
/// </summary>
public partial class LoginWithRecoveryCode : ComponentBase
{
    #region Injected Services

    [Inject] private ApiAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<LoginWithRecoveryCode> Logger { get; set; } = default!;

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
            var apiResult = await AuthService.LoginWithRecoveryCodeAsync(Input.RecoveryCode);

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
                ErrorMessage = result.ErrorMessage ?? "Invalid recovery code.";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during recovery code login.");
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
        [DataType(DataType.Text)]
        [Display(Name = "Recovery Code")]
        public string RecoveryCode { get; set; } = "";
    }

    #endregion
}
