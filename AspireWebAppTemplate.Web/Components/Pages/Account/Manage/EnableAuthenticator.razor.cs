using System.ComponentModel.DataAnnotations;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Manage;

/// <summary>
/// Enable authenticator app page. Loads setup key from the API and verifies TOTP codes.
/// </summary>
public partial class EnableAuthenticator : ComponentBase
{
    #region Injected Services

    [Inject] private ApiAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<EnableAuthenticator> Logger { get; set; } = default!;

    #endregion

    #region State

    private InputModel Input { get; set; } = new();
    private EditContext editContext = default!;
    protected string? SharedKey { get; private set; }
    protected string? AuthenticatorUri { get; private set; }
    protected string[]? RecoveryCodes { get; private set; }
    protected string? StatusMessage { get; set; }
    protected bool IsBusy { get; set; }

    #endregion

    #region Lifecycle

    protected override void OnInitialized()
    {
        editContext = new EditContext(Input);
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var setup = await AuthService.GetAuthenticatorSetupAsync();
            if (setup is not null)
            {
                SharedKey = setup.SharedKey;
                AuthenticatorUri = setup.AuthenticatorUri;
            }
            else
            {
                StatusMessage = "Error: Unable to load authenticator setup.";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading authenticator setup.");
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    #endregion

    #region Event Handlers

    protected async Task OnValidSubmitAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = null;

        try
        {
            var result = await AuthService.VerifyAuthenticatorAsync(Input.Code);
            if (result is null)
            {
                StatusMessage = "Error: Unable to verify authenticator code.";
                return;
            }

            if (result.Succeeded)
            {
                RecoveryCodes = result.RecoveryCodes;
            }
            else
            {
                StatusMessage = "Error: Verification code is invalid.";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error verifying authenticator.");
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    #region Input Model

    private sealed class InputModel
    {
        [Required]
        [StringLength(7, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Text)]
        [Display(Name = "Verification Code")]
        public string Code { get; set; } = "";
    }

    #endregion
}
