using AspireWebAppTemplate.Core.Contracts.Users;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Settings;

/// <summary>
/// Profile settings sub-page allowing authenticated users to update their personal information.
/// Uses instant-save fields — each field saves on blur if the value has changed.
/// Delegates persistence to the API via <see cref="ApiAuthService"/>.
/// </summary>
/// <remarks>
/// LDAP-sourced users have name fields disabled since those are managed by Active Directory.
/// Only PhoneNumber is editable for LDAP users. The display name, first name, and last name
/// fields use a property setter pattern that captures the previous value before saving,
/// enabling automatic rollback on API failure.
/// </remarks>
[Authorize]
public partial class Profile : ComponentBase
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
    /// Structured logger for recording warnings and errors during profile saves.
    /// </summary>
    [Inject] private ILogger<Profile> Logger { get; set; } = default!;

    #endregion

    #region State

    /// <summary>
    /// Whether the page is loading initial data. Controls the <see cref="UI.Components.Shared.PageContent"/> wrapper.
    /// </summary>
    private bool _isLoading = true;

    /// <summary>
    /// Whether the current user's account is sourced from LDAP/Active Directory.
    /// LDAP users have name fields managed externally and cannot edit them here.
    /// </summary>
    private bool _isLdapUser;

    /// <summary>
    /// The user's display name, bound to the profile text field.
    /// </summary>
    private string? _displayName;

    /// <summary>
    /// The original display name loaded from the API, used to detect changes on blur.
    /// </summary>
    private string? _originalDisplayName;

    /// <summary>
    /// The user's first name, bound to the profile text field.
    /// </summary>
    private string? _firstName;

    /// <summary>
    /// The original first name loaded from the API, used to detect changes on blur.
    /// </summary>
    private string? _originalFirstName;

    /// <summary>
    /// The user's last name, bound to the profile text field.
    /// </summary>
    private string? _lastName;

    /// <summary>
    /// The original last name loaded from the API, used to detect changes on blur.
    /// </summary>
    private string? _originalLastName;

    /// <summary>
    /// The user's email address (display-only).
    /// </summary>
    private string? _email;

    /// <summary>
    /// The user's phone number, bound to the profile text field.
    /// </summary>
    private string? _phoneNumber;

    /// <summary>
    /// The original phone number loaded from the API, used to detect changes on blur.
    /// </summary>
    private string? _originalPhoneNumber;

    #endregion 

    #region Lifecycle

    /// <summary>
    /// Loads the current user's profile from the API on page initialization.
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

        var user = result.Data;

        _displayName = user.DisplayName;
        _originalDisplayName = user.DisplayName;
        _firstName = user.FirstName;
        _originalFirstName = user.FirstName;
        _lastName = user.LastName;
        _originalLastName = user.LastName;
        _email = user.Email;
        _phoneNumber = user.PhoneNumber;
        _originalPhoneNumber = user.PhoneNumber;
        _isLdapUser = user.AuthSource == "LDAP";

        _isLoading = false;
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Saves a single profile field when it loses focus, if the value has changed from what was loaded.
    /// Silent on success; shows a snackbar error and reverts the field on failure.
    /// </summary>
    /// <param name="fieldName">The name of the field being saved (DisplayName, FirstName, LastName, PhoneNumber).</param>
    /// <param name="value">The current field value to save.</param>
    private async Task SaveProfileFieldAsync(string fieldName, string? value)
    {
        // Determine if the value actually changed
        var originalValue = fieldName switch
        {
            "DisplayName" => _originalDisplayName,
            "FirstName" => _originalFirstName,
            "LastName" => _originalLastName,
            "PhoneNumber" => _originalPhoneNumber,
            _ => null
        };

        if (value == originalValue) return;

        // For LDAP users, only PhoneNumber is editable
        if (_isLdapUser && fieldName != "PhoneNumber") return;

        try
        {
            var request = new UpdateProfileRequest
            {
                DisplayName = fieldName == "DisplayName" ? value : _originalDisplayName,
                FirstName = fieldName == "FirstName" ? value : _originalFirstName,
                LastName = fieldName == "LastName" ? value : _originalLastName,
                PhoneNumber = fieldName == "PhoneNumber" ? value : _originalPhoneNumber
            };

            // For LDAP users, don't send name fields
            if (_isLdapUser)
            {
                request.DisplayName = null;
                request.FirstName = null;
                request.LastName = null;
            }

            var profileResult = await AuthService.UpdateProfileAsync(request);
            if (!profileResult.Succeeded)
            {
                RevertProfileField(fieldName);
                Snackbar.Add("Failed to save profile. Please try again.", MudBlazor.Severity.Error);
                StateHasChanged();
                return;
            }

            // Update the original value to reflect the successful save
            UpdateOriginalValue(fieldName, value);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error saving profile field {FieldName}.", fieldName);
            RevertProfileField(fieldName);
            Snackbar.Add("Failed to save profile. Please try again.", MudBlazor.Severity.Error);
            StateHasChanged();
        }
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Reverts a profile field to its original loaded value after a failed save attempt.
    /// </summary>
    /// <param name="fieldName">The field name to revert.</param>
    private void RevertProfileField(string fieldName)
    {
        switch (fieldName)
        {
            case "DisplayName":
                _displayName = _originalDisplayName;
                break;
            case "FirstName":
                _firstName = _originalFirstName;
                break;
            case "LastName":
                _lastName = _originalLastName;
                break;
            case "PhoneNumber":
                _phoneNumber = _originalPhoneNumber;
                break;
        }
    }

    /// <summary>
    /// Updates the tracked original value for a profile field after a successful save.
    /// This ensures subsequent blur events don't re-save unchanged values.
    /// </summary>
    /// <param name="fieldName">The field name whose original value should be updated.</param>
    /// <param name="value">The new original value.</param>
    private void UpdateOriginalValue(string fieldName, string? value)
    {
        switch (fieldName)
        {
            case "DisplayName":
                _originalDisplayName = value;
                break;
            case "FirstName":
                _originalFirstName = value;
                break;
            case "LastName":
                _originalLastName = value;
                break;
            case "PhoneNumber":
                _originalPhoneNumber = value;
                break;
        }
    }

    #endregion
}
