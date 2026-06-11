using System.ComponentModel.DataAnnotations;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Manage;

/// <summary>
/// Rename passkey page. Calls the API to rename a passkey.
/// </summary>
public partial class RenamePasskey : ComponentBase
{
    #region Injected Services

    [Inject] private ApiAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<RenamePasskey> Logger { get; set; } = default!;

    #endregion

    #region Parameters

    [Parameter]
    public string? Id { get; set; }

    #endregion

    #region State

    private InputModel Input { get; set; } = new();
    private EditContext editContext = default!;
    protected string HeaderTitle { get; private set; } = "Rename Passkey";
    protected string? StatusMessage { get; set; }
    protected bool IsBusy { get; set; }

    #endregion

    #region Lifecycle

    protected override void OnInitialized()
    {
        editContext = new EditContext(Input);
    }

    #endregion

    #region Event Handlers

    protected async Task OnRenameAsync()
    {
        if (IsBusy || string.IsNullOrEmpty(Id)) return;
        IsBusy = true;
        StatusMessage = null;

        try
        {
            var (success, error) = await AuthService.RenamePasskeyAsync(Id, Input.Name);
            if (success)
            {
                NavigationManager.NavigateTo("Account/Manage/Passkeys");
            }
            else
            {
                StatusMessage = error ?? "Error renaming passkey.";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error renaming passkey.");
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
        [StringLength(200, ErrorMessage = "Passkey names must be no longer than {1} characters.")]
        public string Name { get; set; } = "";
    }

    #endregion
}
