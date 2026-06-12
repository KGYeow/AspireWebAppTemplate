using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Core.Contracts;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Manage;

/// <summary>
/// Manage external logins page. Lists and manages external logins via the API.
/// </summary>
public partial class ExternalLogins : ComponentBase
{
    public const string LinkLoginCallbackAction = "LinkLoginCallback";

    #region Injected Services

    [Inject] private ApiAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<ExternalLogins> Logger { get; set; } = default!;

    #endregion

    #region State

    protected List<ExternalLoginInfoDto>? CurrentLogins { get; private set; }
    protected bool ShowRemoveButton { get; private set; }
    protected string? StatusMessage { get; set; }

    #endregion

    #region Lifecycle

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var result = await AuthService.GetExternalLoginsAsync();
            if (result.Succeeded && result.Data is not null)
            {
                CurrentLogins = result.Data.CurrentLogins;
                ShowRemoveButton = result.Data.ShowRemoveButton;
            }
            else
            {
                StatusMessage = "Error: Unable to load external logins.";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading external logins.");
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    #endregion

    #region Event Handlers

    protected async Task RemoveLoginAsync(string loginProvider, string providerKey)
    {
        try
        {
            var result = await AuthService.RemoveExternalLoginAsync(loginProvider, providerKey);
            if (result.Succeeded)
            {
                StatusMessage = "The external login was removed.";
                var refreshResult = await AuthService.GetExternalLoginsAsync();
                if (refreshResult.Succeeded && refreshResult.Data is not null)
                {
                    CurrentLogins = refreshResult.Data.CurrentLogins;
                    ShowRemoveButton = refreshResult.Data.ShowRemoveButton;
                }
            }
            else
            {
                StatusMessage = $"Error: {result.Error}";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error removing external login.");
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    #endregion
}
