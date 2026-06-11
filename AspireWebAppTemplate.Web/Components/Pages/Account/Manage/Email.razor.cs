using System.ComponentModel.DataAnnotations;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Manage;

/// <summary>
/// Manage email page. Delegates email operations to the API via <see cref="ApiAuthService"/>.
/// </summary>
public partial class Email : ComponentBase
{
    #region Injected Services

    [Inject] private ApiAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<Email> Logger { get; set; } = default!;

    #endregion

    #region State

    private InputModel Input { get; set; } = new();
    private EditContext editContext = default!;
    protected string? CurrentEmail { get; private set; }
    protected bool IsEmailConfirmed { get; private set; }
    protected string? StatusMessage { get; set; }
    protected bool IsBusy { get; set; }

    #endregion

    #region Lifecycle

    protected override void OnInitialized()
    {
        editContext = new EditContext(Input);
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var emailInfo = await AuthService.GetEmailInfoAsync();
            if (emailInfo is not null)
            {
                CurrentEmail = emailInfo.Email;
                IsEmailConfirmed = emailInfo.IsEmailConfirmed;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading email info.");
            StatusMessage = "Error: Unable to load email information.";
        }
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
            var (success, error) = await AuthService.ChangeEmailAsync(Input.NewEmail!);
            if (success)
            {
                StatusMessage = "Your email has been changed. Please verify your new email address.";
                // Refresh email info
                var emailInfo = await AuthService.GetEmailInfoAsync();
                if (emailInfo is not null)
                {
                    CurrentEmail = emailInfo.Email;
                    IsEmailConfirmed = emailInfo.IsEmailConfirmed;
                }
                Input.NewEmail = null;
            }
            else
            {
                StatusMessage = $"Error: {error}";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error changing email.");
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected async Task OnSendEmailVerificationAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = null;

        try
        {
            var (success, error) = await AuthService.SendVerificationEmailAsync();
            StatusMessage = success
                ? "Verification email sent. Please check your email."
                : $"Error: {error}";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error sending verification email.");
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
        [EmailAddress]
        [Display(Name = "New email")]
        public string? NewEmail { get; set; }
    }

    #endregion
}
