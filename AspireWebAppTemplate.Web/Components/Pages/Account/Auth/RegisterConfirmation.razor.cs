using Microsoft.AspNetCore.Components;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Auth;

/// <summary>
/// Displays a confirmation message after successful registration, prompting the
/// user to check their email. In the Aspire architecture, email confirmation
/// is handled by the API.
/// </summary>
public partial class RegisterConfirmation : ComponentBase
{
    #region Injected Services

    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    #endregion

    #region Query Parameters

    [SupplyParameterFromQuery]
    protected string? Email { get; set; }

    [SupplyParameterFromQuery]
    private string? ReturnUrl { get; set; }

    #endregion

    #region State

    protected string? EmailConfirmationLink { get; private set; }
    protected string? StatusMessage { get; private set; }

    #endregion

    #region Lifecycle

    protected override Task OnInitializedAsync()
    {
        if (Email is null)
        {
            NavigationManager.NavigateTo("", forceLoad: true);
        }

        return Task.CompletedTask;
    }

    #endregion
}
