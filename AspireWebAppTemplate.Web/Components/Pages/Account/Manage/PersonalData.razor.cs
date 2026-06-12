using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Manage;

/// <summary>
/// Personal data page. Provides download functionality via the API.
/// </summary>
public partial class PersonalData : ComponentBase
{
    #region Injected Services

    [Inject] private ApiAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private ILogger<PersonalData> Logger { get; set; } = default!;

    #endregion

    #region State

    protected string? StatusMessage { get; set; }
    protected bool IsBusy { get; set; }

    #endregion

    #region Event Handlers

    protected async Task OnDownloadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = null;

        try
        {
            var result = await AuthService.DownloadPersonalDataAsync();
            if (result.Succeeded && result.Data is not null)
            {
                // Trigger browser download via JS interop
                using var streamRef = new DotNetStreamReference(new MemoryStream(result.Data));
                await JS.InvokeVoidAsync("downloadFileFromStream", "PersonalData.json", streamRef);
            }
            else
            {
                StatusMessage = "Error: Unable to download personal data.";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error downloading personal data.");
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion
}
