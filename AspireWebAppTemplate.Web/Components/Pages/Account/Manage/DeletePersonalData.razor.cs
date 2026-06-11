using System.ComponentModel.DataAnnotations;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Manage;

/// <summary>
/// Delete personal data page. Confirms with password then deletes account via the API.
/// </summary>
public partial class DeletePersonalData : ComponentBase
{
    #region Injected Services

    [Inject] private ApiAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<DeletePersonalData> Logger { get; set; } = default!;

    #endregion

    #region State

    private InputModel Input { get; set; } = new();
    private EditContext editContext = default!;
    protected string? StatusMessage { get; set; }
    protected bool IsBusy { get; set; }
    protected bool RequirePassword { get; private set; } = true;

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
        StatusMessage = null;

        try
        {
            var (success, error) = await AuthService.DeleteAccountAsync(Input.Password);
            if (success)
            {
                await AuthService.LogoutAsync();
                NavigationManager.NavigateTo("Account/Login", forceLoad: true);
            }
            else
            {
                StatusMessage = error ?? "Error deleting account.";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deleting account.");
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
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";
    }

    #endregion
}
