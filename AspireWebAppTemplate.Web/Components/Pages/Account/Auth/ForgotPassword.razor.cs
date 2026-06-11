using System.ComponentModel.DataAnnotations;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Auth;

public partial class ForgotPassword : ComponentBase
{
    [Inject] private ApiAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<ForgotPassword> Logger { get; set; } = default!;

    private InputModel Input { get; set; } = new();
    private EditContext editContext = default!;
    protected string? ErrorMessage { get; private set; }
    protected bool IsBusy { get; private set; }

    protected override void OnInitialized()
    {
        editContext = new EditContext(Input);
    }

    protected async Task OnValidSubmitAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            // Call API — it handles email sending and returns success regardless
            // of whether the user exists (for security)
            await AuthService.ForgotPasswordAsync(Input.Email);

            // Always navigate to confirmation (don't reveal account existence)
            NavigationManager.NavigateTo("Account/ForgotPasswordConfirmation", forceLoad: true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during password reset request.");
            // Still navigate to confirmation for security
            NavigationManager.NavigateTo("Account/ForgotPasswordConfirmation", forceLoad: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected void ClearError() => ErrorMessage = null;

    private sealed class InputModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;
    }
}
