using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Core.Contracts;
using AspireWebAppTemplate.Core.Contracts.Auth;
using AspireWebAppTemplate.Core.Contracts.AuditLog;
using AspireWebAppTemplate.Core.Contracts.Roles;
using AspireWebAppTemplate.Core.Contracts.Users;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Manage;

/// <summary>
/// Manage passkeys page. Lists and manages passkeys via the API.
/// </summary>
public partial class Passkeys : ComponentBase
{
    internal const int MaxPasskeyCount = 100;

    #region Injected Services

    [Inject] private ApiAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<Passkeys> Logger { get; set; } = default!;

    #endregion

    #region State

    protected List<PasskeyInfoDto>? CurrentPasskeys { get; private set; }
    protected string? StatusMessage { get; set; }

    #endregion

    #region Lifecycle

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var result = await AuthService.GetPasskeysAsync();
            if (result.Succeeded)
                CurrentPasskeys = result.Data;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading passkeys.");
            StatusMessage = "Error: Unable to load passkeys.";
        }
    }

    #endregion

    #region Event Handlers

    protected async Task DeletePasskeyAsync(string credentialId)
    {
        try
        {
            var result = await AuthService.DeletePasskeyAsync(credentialId);
            if (result.Succeeded)
            {
                StatusMessage = "Passkey deleted successfully.";
                var refreshResult = await AuthService.GetPasskeysAsync();
                if (refreshResult.Succeeded)
                    CurrentPasskeys = refreshResult.Data;
            }
            else
            {
                StatusMessage = $"Error: {result.Error}";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deleting passkey.");
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    #endregion
}
