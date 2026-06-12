using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Manage;

/// <summary>
/// Reset authenticator key page. Calls the API to reset the key then navigates to setup.
/// </summary>
public partial class ResetAuthenticator : ComponentBase
{
    #region Injected Services

    [Inject] private ApiAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<ResetAuthenticator> Logger { get; set; } = default!;

    #endregion

    #region State

    protected string? StatusMessage { get; set; }
    protected bool IsBusy { get; set; }

    #endregion

    #region Event Handlers

    protected async Task OnSubmitAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = null;

        try
        {
            var result = await AuthService.ResetAuthenticatorAsync();
            if (result.Succeeded)
            {
                NavigationManager.NavigateTo("Account/Manage/EnableAuthenticator");
            }
            else
            {
                StatusMessage = result.Error ?? "Error resetting authenticator.";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error resetting authenticator.");
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion
}
