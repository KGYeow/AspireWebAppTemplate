using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Manage;

/// <summary>
/// Generate new 2FA recovery codes page. Calls the API to generate codes.
/// </summary>
public partial class GenerateRecoveryCodes : ComponentBase
{
    #region Injected Services

    [Inject] private ApiAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<GenerateRecoveryCodes> Logger { get; set; } = default!;

    #endregion

    #region State

    protected string[]? RecoveryCodes { get; private set; }
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
            var codes = await AuthService.GenerateRecoveryCodesAsync();
            if (codes is not null)
            {
                RecoveryCodes = codes;
            }
            else
            {
                StatusMessage = "Error generating recovery codes. 2FA may not be enabled.";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error generating recovery codes.");
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion
}
