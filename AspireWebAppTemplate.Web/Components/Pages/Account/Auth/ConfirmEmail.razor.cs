using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Auth;

public partial class ConfirmEmail : ComponentBase
{
    [Inject] private ApiAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<ConfirmEmail> Logger { get; set; } = default!;

    [SupplyParameterFromQuery]
    private string? UserId { get; set; }

    [SupplyParameterFromQuery]
    private string? Code { get; set; }

    protected bool IsSuccess { get; private set; }
    protected string? StatusMessage { get; private set; }

    protected override async Task OnInitializedAsync()
    {
        if (UserId is null || Code is null)
        {
            NavigationManager.NavigateTo("", forceLoad: true);
            return;
        }

        try
        {
            var result = await AuthService.ConfirmEmailAsync(UserId, Code);
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
}
