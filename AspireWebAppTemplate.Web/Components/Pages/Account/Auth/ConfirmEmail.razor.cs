using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Application.Features.Template.Authentication;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Auth;

/// <summary>
/// Email confirmation page. Validates the confirmation token via the API
/// and displays success or failure to the user.
/// </summary>
public partial class ConfirmEmail : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// Provides authentication operations via the API.
    /// </summary>
    [Inject] private ApiAuthService AuthService { get; set; } = default!;

    /// <summary>
    /// Provides navigation actions.
    /// </summary>
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    /// <summary>
    /// Structured logger for recording events.
    /// </summary>
    [Inject] private ILogger<ConfirmEmail> Logger { get; set; } = default!;

    #endregion

    #region Query Parameters

    /// <summary>
    /// The user ID from the confirmation link.
    /// </summary>
    [SupplyParameterFromQuery]
    private string? UserId { get; set; }

    /// <summary>
    /// The Base64Url-encoded confirmation code from the email link.
    /// </summary>
    [SupplyParameterFromQuery]
    private string? Code { get; set; }

    #endregion

    #region State

    /// <summary>
    /// Whether the email confirmation was successful.
    /// </summary>
    protected bool IsSuccess { get; private set; }

    /// <summary>
    /// Status message displayed to the user on failure.
    /// </summary>
    protected string? StatusMessage { get; private set; }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Validates the confirmation token on page load.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        if (UserId is null || Code is null)
        {
            NavigationManager.NavigateTo("", forceLoad: true);
            return;
        }

        try
        {
            var result = await AuthService.ConfirmEmailAsync(new ConfirmEmailRequest { UserId = UserId, Code = Code });
            if (result.Succeeded)
            {
                IsSuccess = true;
            }
            else
            {
                StatusMessage = result.Error ?? "Error confirming your email. The link may have expired or already been used.";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error confirming email.");
            StatusMessage = "Error confirming your email. Please try again.";
        }
    }

    #endregion
}
