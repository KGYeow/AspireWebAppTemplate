using System.ComponentModel.DataAnnotations;
using AspireWebAppTemplate.Core.Contracts.Users;
using AspireWebAppTemplate.Core.Utilities.Attributes;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Manage;

/// <summary>
/// Manage account profile page. Allows the user to view their username
/// and update their phone number. Delegates to the API via <see cref="ApiAuthService"/>.
/// </summary>
public partial class Index : ComponentBase
{
    #region Injected Services

    [Inject] private ApiAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<Index> Logger { get; set; } = default!;

    #endregion

    #region State

    protected string? Username { get; private set; }
    private string? phoneNumber;
    private InputModel Input { get; set; } = new();
    private EditContext editContext = default!;
    protected string? StatusMessage { get; set; }
    protected bool IsBusy { get; private set; }

    #endregion

    #region Lifecycle

    protected override void OnInitialized()
    {
        editContext = new EditContext(Input);
    }

    protected override async Task OnInitializedAsync()
    {
        var result = await AuthService.GetCurrentUserAsync();
        if (!result.Succeeded || result.Data is null)
        {
            NavigationManager.NavigateTo("Account/InvalidUser", forceLoad: true);
            return;
        }

        var user = result.Data;
        Username = user.UserName;
        phoneNumber = user.PhoneNumber;
        Input.PhoneNumber = phoneNumber;
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
            if (Input.PhoneNumber != phoneNumber)
            {
                var result = await AuthService.UpdateProfileAsync(
                    new UpdateProfileRequest { PhoneNumber = Input.PhoneNumber });

                if (!result.Succeeded)
                {
                    StatusMessage = $"Error: {result.Error}";
                    return;
                }

                phoneNumber = Input.PhoneNumber;
            }

            StatusMessage = "Your profile has been updated.";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error updating profile.");
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
        [OptionalPhone]
        [Display(Name = "Phone number")]
        public string? PhoneNumber { get; set; }
    }

    #endregion
}
