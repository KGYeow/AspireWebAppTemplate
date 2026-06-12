using System.ComponentModel.DataAnnotations;
using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Core.Contracts;
using AspireWebAppTemplate.Core.Utilities;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Profile;

/// <summary>
/// Profile page allowing authenticated users to view and edit their personal
/// information. Delegates all persistence to the API via <see cref="ApiAuthService"/>.
/// </summary>
public partial class Index : ComponentBase
{
    #region Injected Services

    [Inject] private ApiAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<Index> Logger { get; set; } = default!;

    #endregion

    #region State

    protected UserDto? User { get; private set; }
    private InputModel Input { get; set; } = new();
    private EditContext editContext = default!;
    protected string? StatusMessage { get; set; }
    protected bool IsEditing { get; set; }
    protected bool IsBusy { get; private set; }
    protected string AvatarText => GetAvatarText();
    protected bool IsLdapUser => User?.AuthSource == "LDAP";

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

        User = result.Data;
        Input.DisplayName = User.DisplayName;
        Input.FirstName = User.FirstName;
        Input.LastName = User.LastName;
        Input.PhoneNumber = User.PhoneNumber;
    }

    #endregion

    #region Event Handlers

    private void EnterEditMode()
    {
        IsEditing = true;
        StatusMessage = null;
        Input.DisplayName = User?.DisplayName;
        Input.FirstName = User?.FirstName;
        Input.LastName = User?.LastName;
        Input.PhoneNumber = User?.PhoneNumber;
    }

    private void CancelEdit()
    {
        IsEditing = false;
        StatusMessage = null;
        Input.DisplayName = User?.DisplayName;
        Input.FirstName = User?.FirstName;
        Input.LastName = User?.LastName;
        Input.PhoneNumber = User?.PhoneNumber;
    }

    protected async Task OnValidSubmitAsync()
    {
        if (IsBusy || User is null) return;
        IsBusy = true;
        StatusMessage = null;

        try
        {
            var request = new UpdateProfileRequest
            {
                PhoneNumber = Input.PhoneNumber
            };

            // For non-LDAP users, also update name fields
            if (!IsLdapUser)
            {
                request.DisplayName = Input.DisplayName;
                request.FirstName = Input.FirstName;
                request.LastName = Input.LastName;
            }

            var profileResult = await AuthService.UpdateProfileAsync(request);
            if (!profileResult.Succeeded)
            {
                StatusMessage = $"Error: {profileResult.Error}";
                return;
            }

            // Refresh user data
            var refreshResult = await AuthService.GetCurrentUserAsync();
            if (refreshResult.Succeeded && refreshResult.Data is not null)
                User = refreshResult.Data;
            StatusMessage = "Your profile has been updated.";
            IsEditing = false;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating profile.");
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    #region Helpers

    private string GetAvatarText()
    {
        if (!string.IsNullOrEmpty(User?.DisplayName))
            return User.DisplayName[..1].ToUpperInvariant();
        if (!string.IsNullOrEmpty(User?.UserName))
            return User.UserName[..1].ToUpperInvariant();
        return "?";
    }

    #endregion

    #region Input Model

    private sealed class InputModel
    {
        [Display(Name = "Display Name")]
        [MaxLength(100)]
        public string? DisplayName { get; set; }

        [Display(Name = "First Name")]
        [MaxLength(100)]
        public string? FirstName { get; set; }

        [Display(Name = "Last Name")]
        [MaxLength(100)]
        public string? LastName { get; set; }

        [OptionalPhone]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }
    }

    #endregion
}
