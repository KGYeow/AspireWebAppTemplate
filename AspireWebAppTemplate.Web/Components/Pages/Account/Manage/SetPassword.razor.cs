using System.ComponentModel.DataAnnotations;
using AspireWebAppTemplate.Core.Contracts;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Manage;

/// <summary>
/// Set password page for users who registered via external login and have no local password.
/// Delegates to the API via <see cref="ApiAuthService"/>.
/// </summary>
public partial class SetPassword : ComponentBase
{
    #region Injected Services

    [Inject] private ApiAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    #endregion

    #region State

    private InputModel Input { get; set; } = new();
    private EditContext editContext = default!;
    protected string? StatusMessage { get; set; }
    protected bool IsBusy { get; set; }
    protected bool IsPasswordVisible { get; set; }
    protected bool IsConfirmVisible { get; set; }

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
            var request = new SetPasswordRequest { NewPassword = Input.NewPassword! };
            var error = await AuthService.SetPasswordAsync(request);

            if (error is not null)
            {
                StatusMessage = $"Error: {error}";
                return;
            }

            StatusMessage = "Your password has been set.";
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
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm new password")]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        public string? ConfirmPassword { get; set; }
    }

    #endregion
}
