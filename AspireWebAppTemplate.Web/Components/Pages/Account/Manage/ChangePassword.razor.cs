using System.ComponentModel.DataAnnotations;
using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Application.Contracts.Auth;
using AspireWebAppTemplate.Application.Contracts.AuditLog;
using AspireWebAppTemplate.Application.Contracts.Roles;
using AspireWebAppTemplate.Application.Contracts.Users;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Manage;

/// <summary>
/// Change password page. Delegates the password change to the API
/// via <see cref="ApiAuthService"/>.
/// </summary>
public partial class ChangePassword : ComponentBase
{
    #region Injected Services

    [Inject] private ApiAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<ChangePassword> Logger { get; set; } = default!;

    #endregion

    #region State

    private InputModel Input { get; set; } = new();
    private EditContext editContext = default!;
    protected string? StatusMessage { get; set; }
    protected bool IsBusy { get; set; }
    protected bool IsOldPasswordVisible { get; set; }
    protected bool IsNewPasswordVisible { get; set; }
    protected bool IsConfirmPasswordVisible { get; set; }

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
            var request = new ChangePasswordRequest
            {
                CurrentPassword = Input.OldPassword,
                NewPassword = Input.NewPassword
            };

            var result = await AuthService.ChangePasswordAsync(request);
            if (!result.Succeeded)
            {
                StatusMessage = $"Error: {result.Error}";
                return;
            }

            Logger.LogInformation("User changed their password successfully.");
            StatusMessage = "Your password has been changed.";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error changing password.");
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
        [DataType(DataType.Password)]
        [Display(Name = "Current password")]
        public string OldPassword { get; set; } = "";

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string NewPassword { get; set; } = "";

        [DataType(DataType.Password)]
        [Display(Name = "Confirm new password")]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = "";
    }

    #endregion
}
