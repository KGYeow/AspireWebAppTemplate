using System.ComponentModel.DataAnnotations;
using System.Text;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Auth;

public partial class ResetPassword : ComponentBase
{
    [Inject] private ApiAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<ResetPassword> Logger { get; set; } = default!;

    [SupplyParameterFromQuery]
    private string? Code { get; set; }

    private InputModel Input { get; set; } = new();
    private EditContext editContext = default!;
    private string decodedCode = string.Empty;
    protected string? ErrorMessage { get; private set; }
    protected bool IsBusy { get; private set; }
    protected bool IsPasswordVisible { get; private set; }
    protected bool IsConfirmPasswordVisible { get; private set; }

    protected override void OnInitialized()
    {
        if (Code is null)
        {
            NavigationManager.NavigateTo("Account/InvalidPasswordReset", forceLoad: true);
            return;
        }

        decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(Code));
        editContext = new EditContext(Input);
    }

    protected async Task OnValidSubmitAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var (success, error) = await AuthService.ResetPasswordAsync(Input.Email, decodedCode, Input.Password);
            if (success)
            {
                NavigationManager.NavigateTo("Account/ResetPasswordConfirmation", forceLoad: true);
            }
            else
            {
                ErrorMessage = error ?? "Failed to reset password. The link may have expired.";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during password reset.");
            ErrorMessage = $"Unexpected error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected void TogglePasswordVisibility() => IsPasswordVisible = !IsPasswordVisible;
    protected void ToggleConfirmPasswordVisibility() => IsConfirmPasswordVisible = !IsConfirmPasswordVisible;
    protected void ClearError() => ErrorMessage = null;

    private sealed class InputModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
