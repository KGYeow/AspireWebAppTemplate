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
    [Inject] private ILogger<LoginWithRecoveryCode> Logger { get; set; } = default!;

    #endregion

    #region Query Parameters

    /// <summary>
    /// The URL to redirect to after successful authentication.
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
    /// Verifies the recovery code via the API and navigates based on the result.
    /// </summary>
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

    /// <summary>
    /// Clears the current error message.
    /// </summary>
    protected void ClearError() => ErrorMessage = null;

    #endregion

    #region Input Model

    /// <summary>
    /// Form model for the recovery code login page.
    /// </summary>
    private sealed class InputModel
    {
        /// <summary>
        /// The recovery code entered by the user.
        /// </summary>
        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "Recovery Code")]
        public string RecoveryCode { get; set; } = "";
    }

    #endregion
}
