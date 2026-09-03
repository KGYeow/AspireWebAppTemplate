using System.ComponentModel.DataAnnotations;
using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Application.Features.Template.Authentication;
using AspireWebAppTemplate.Application.Features.Template.AuditLog;
using AspireWebAppTemplate.Application.Features.Template.Roles;
using AspireWebAppTemplate.Application.Features.Template.Users;
using AspireWebAppTemplate.Domain.Attributes;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Profile;

/// <summary>
/// Profile page allowing authenticated users to view and edit their personal
/// information. Supports view mode and edit mode with form validation.
/// Delegates all persistence to the API via <see cref="ApiAuthService"/>.
/// </summary>
/// <remarks>
/// LDAP-sourced users have name fields disabled in edit mode since those are
/// managed by Active Directory. Only PhoneNumber is editable for LDAP users.
/// </remarks>
public partial class Index : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// HTTP client service for auth operations including profile retrieval and updates.
    /// </summary>
    [Inject] private ApiAuthService AuthService { get; set; } = default!;

    /// <summary>
    /// Provides navigation actions (e.g., redirecting to InvalidUser on load failure).
    /// </summary>
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    /// <summary>
    /// Structured logger for recording errors during profile operations.
    /// </summary>
    [Inject] private ILogger<Index> Logger { get; set; } = default!;

    #endregion

    #region State

    /// <summary>
    /// The currently loaded user profile data. Null before initial load completes.
    /// </summary>
    protected UserDto? User { get; private set; }

    /// <summary>
    /// The form input model bound to the edit form fields.
    /// </summary>
    private InputModel Input { get; set; } = new();

    /// <summary>
    /// The edit context for form validation tracking.
    /// </summary>
    private EditContext editContext = default!;

    /// <summary>
    /// Status message displayed after a save attempt (success or error).
    /// </summary>
    protected string? StatusMessage { get; set; }

    /// <summary>
    /// Whether the page is currently in edit mode (showing the form) vs view mode.
    /// </summary>
    protected bool IsEditing { get; set; }

    /// <summary>
    /// Whether a save operation is currently in progress. Used to disable the Save button.
    /// </summary>
    protected bool IsBusy { get; private set; }

    /// <summary>
    /// Whether the page is loading initial data. Controls the <see cref="UI.Components.Shared.PageContent"/> wrapper.
    /// </summary>
    protected bool IsLoading { get; private set; } = true;

    /// <summary>
    /// The first character of the user's display name (or username), used for the avatar circle.
    /// </summary>
    protected string AvatarText => GetAvatarText();

    /// <summary>
    /// Whether the current user's account is sourced from LDAP/Active Directory.
    /// LDAP users have name fields managed externally and cannot edit them here.
    /// </summary>
    protected bool IsLdapUser => User?.AuthSource == "LDAP";

    #endregion

    #region Lifecycle

    /// <summary>
    /// Initializes the edit context for form validation on first render.
    /// </summary>
    protected override void OnInitialized()
    {
        editContext = new EditContext(Input);
    }

    /// <summary>
    /// Loads the current user's profile data from the API on page initialization.
    /// Redirects to InvalidUser page if the user cannot be resolved.
    /// </summary>
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
        IsLoading = false;
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Enters edit mode by switching from the read-only view to the editable form.
    /// Populates the input model with current user values.
    /// </summary>
    private void EnterEditMode()
    {
        IsEditing = true;
        StatusMessage = null;
        Input.DisplayName = User?.DisplayName;
        Input.FirstName = User?.FirstName;
        Input.LastName = User?.LastName;
        Input.PhoneNumber = User?.PhoneNumber;
    }

    /// <summary>
    /// Cancels edit mode and reverts the input model back to the current user values.
    /// </summary>
    private void CancelEdit()
    {
        IsEditing = false;
        StatusMessage = null;
        Input.DisplayName = User?.DisplayName;
        Input.FirstName = User?.FirstName;
        Input.LastName = User?.LastName;
        Input.PhoneNumber = User?.PhoneNumber;
    }

    /// <summary>
    /// Handles the form submission after validation passes.
    /// Sends the profile update request to the API and refreshes the displayed user data.
    /// </summary>
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

            // Refresh user data to reflect the saved changes in view mode
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

    /// <summary>
    /// Computes the avatar initial text from the user's display name or username.
    /// Returns "?" if neither is available.
    /// </summary>
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

    /// <summary>
    /// Form input model for profile editing with validation attributes.
    /// </summary>
    private sealed class InputModel
    {
        /// <summary>
        /// The user's preferred display name. Max 100 characters.
        /// </summary>
        [Display(Name = "Display Name")]
        [MaxLength(100)]
        public string? DisplayName { get; set; }

        /// <summary>
        /// The user's first/given name. Max 100 characters.
        /// </summary>
        [Display(Name = "First Name")]
        [MaxLength(100)]
        public string? FirstName { get; set; }

        /// <summary>
        /// The user's last/family name. Max 100 characters.
        /// </summary>
        [Display(Name = "Last Name")]
        [MaxLength(100)]
        public string? LastName { get; set; }

        /// <summary>
        /// The user's phone number. Validated with <see cref="OptionalPhoneAttribute"/>.
        /// </summary>
        [OptionalPhone]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }
    }

    #endregion
}
