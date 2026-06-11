using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Manage;

/// <summary>
/// Disable two-factor authentication page. Calls the API to disable 2FA.
/// </summary>
public partial class Disable2fa : ComponentBase
{
    #region Injected Services

    [Inject] private ApiAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<Disable2fa> Logger { get; set; } = default!;

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
            var (success, error) = await AuthService.Disable2faAsync();
            if (success)
            {
                NavigationManager.NavigateTo("Account/Manage/TwoFactorAuthentication");
            }
            else
            {
                StatusMessage = error ?? "Error disabling 2FA.";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error disabling 2FA.");
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion
}
