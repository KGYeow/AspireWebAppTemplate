using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Manage;

/// <summary>
/// Two-factor authentication overview page. Loads 2FA status from the API.
/// </summary>
public partial class TwoFactorAuthentication : ComponentBase
{
    #region Injected Services

    [Inject] private ApiAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<TwoFactorAuthentication> Logger { get; set; } = default!;

    #endregion

    #region State

    protected bool HasAuthenticator { get; private set; }
    protected int RecoveryCodesLeft { get; private set; }
    protected bool Is2faEnabled { get; private set; }
    protected string? StatusMessage { get; set; }

    #endregion

    #region Lifecycle

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var status = await AuthService.Get2faStatusAsync();
            if (status is not null)
            {
                HasAuthenticator = status.HasAuthenticator;
                Is2faEnabled = status.Is2faEnabled;
                RecoveryCodesLeft = status.RecoveryCodesLeft;
            }
            else
            {
                StatusMessage = "Unable to load 2FA status.";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading 2FA status.");
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    #endregion
}
