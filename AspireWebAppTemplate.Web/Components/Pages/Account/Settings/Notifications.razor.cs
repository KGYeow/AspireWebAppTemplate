using AspireWebAppTemplate.Application.Contracts.Notifications;
using AspireWebAppTemplate.Application.Contracts.Users;
using AspireWebAppTemplate.Domain.Enums;
using AspireWebAppTemplate.Web.Abstractions;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Settings;

/// <summary>
/// Notifications settings sub-page allowing authenticated users to configure which
/// notifications they receive and through which channels (In-App and Email).
/// Changes are saved instantly to the API via <see cref="ApiNotificationService"/>.
/// </summary>
/// <remarks>
/// Each notification category displays two toggles (In-App and Email). On toggle change,
/// the new value is immediately persisted. On failure, the toggle reverts to its previous
/// state and a Snackbar error is displayed.
/// </remarks>
[Authorize]
public partial class Notifications : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// Typed HttpClient service for notification preference operations (get/update).
    /// </summary>
    [Inject] private ApiNotificationService NotificationService { get; set; } = default!;

    /// <summary>
    /// Typed HttpClient service for user preference operations (get current user, update preferences).
    /// </summary>
    [Inject] private ApiAuthService AuthService { get; set; } = default!;

    /// <summary>
    /// Per-circuit notification context for updating the popup preference in real time.
    /// </summary>
    [Inject] private INotificationContext NotificationContext { get; set; } = default!;

    /// <summary>
    /// Provides navigation actions (e.g., redirecting to InvalidUser on load failure).
    /// </summary>
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    /// <summary>
    /// Structured logger for recording warnings and errors during preference saves.
    /// </summary>
    [Inject] private ILogger<Notifications> Logger { get; set; } = default!;

    #endregion

    #region State

    /// <summary>
    /// The list of notification preferences for all categories, loaded from the API.
    /// </summary>
    private List<NotificationPreferenceDto> _notificationPreferences = [];

    /// <summary>
    /// Whether pop-up notifications are enabled globally for this user.
    /// </summary>
    private bool _notificationPopupsEnabled = true;

    /// <summary>
    /// Whether the page is loading its data.
    /// </summary>
    private bool _isLoading = true;

    #endregion

    #region Lifecycle

    /// <summary>
    /// Loads notification delivery preferences for all categories and the global popup setting from the API.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        await LoadPopupPreferenceAsync();
        await LoadNotificationPreferencesAsync();
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles the global pop-up notifications toggle change.
    /// Immediately persists the new value via the API and updates the shared NotificationContext
    /// so the change takes effect across the layout without a page refresh.
    /// On failure, reverts the toggle and displays a Snackbar error.
    /// </summary>
    /// <param name="newValue">The new pop-up enabled value.</param>
    private async Task HandlePopupToggle(bool newValue)
    {
        var previousValue = _notificationPopupsEnabled;
        _notificationPopupsEnabled = newValue;

        try
        {
            var result = await AuthService.UpdatePreferencesAsync(
                new UpdatePreferencesRequest { NotificationPopupsEnabled = newValue });

            if (!result.Succeeded)
            {
                _notificationPopupsEnabled = previousValue;
                Snackbar.Add("Failed to save notification preference. Please try again.", Severity.Error);
                return;
            }

            // Update the shared per-circuit context so NotificationBell picks up the change immediately.
            NotificationContext.NotificationPopupsEnabled = newValue;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error saving pop-up notification preference.");
            _notificationPopupsEnabled = previousValue;
            Snackbar.Add("Failed to save notification preference. Please try again.", Severity.Error);
        }
    }

    /// <summary>
    /// Handles the In-App notification toggle change for a specific category.
    /// Immediately persists the new value via the API. On failure, reverts the toggle
    /// to its previous state and displays a Snackbar error.
    /// </summary>
    /// <param name="pref">The preference DTO being toggled.</param>
    /// <param name="newValue">The new In-App enabled value.</param>
    private async Task HandleInAppToggle(NotificationPreferenceDto pref, bool newValue)
    {
        var previousValue = pref.InAppEnabled;
        pref.InAppEnabled = newValue;

        var request = new UpdateNotificationPreferenceRequest
        {
            Category = pref.Category,
            InAppEnabled = newValue,
            EmailEnabled = pref.EmailEnabled
        };

        try
        {
            var result = await NotificationService.UpdatePreferenceAsync(request);
            if (!result.Succeeded)
            {
                pref.InAppEnabled = previousValue;
                Snackbar.Add("Failed to save notification preference. Please try again.", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error saving In-App notification preference for {Category}.", pref.Category);
            pref.InAppEnabled = previousValue;
            Snackbar.Add("Failed to save notification preference. Please try again.", Severity.Error);
        }
    }

    /// <summary>
    /// Handles the Email notification toggle change for a specific category.
    /// Immediately persists the new value via the API. On failure, reverts the toggle
    /// to its previous state and displays a Snackbar error.
    /// </summary>
    /// <param name="pref">The preference DTO being toggled.</param>
    /// <param name="newValue">The new Email enabled value.</param>
    private async Task HandleEmailToggle(NotificationPreferenceDto pref, bool newValue)
    {
        var previousValue = pref.EmailEnabled;
        pref.EmailEnabled = newValue;

        var request = new UpdateNotificationPreferenceRequest
        {
            Category = pref.Category,
            InAppEnabled = pref.InAppEnabled,
            EmailEnabled = newValue
        };

        try
        {
            var result = await NotificationService.UpdatePreferenceAsync(request);
            if (!result.Succeeded)
            {
                pref.EmailEnabled = previousValue;
                Snackbar.Add("Failed to save notification preference. Please try again.", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error saving Email notification preference for {Category}.", pref.Category);
            pref.EmailEnabled = previousValue;
            Snackbar.Add("Failed to save notification preference. Please try again.", Severity.Error);
        }
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Loads the global pop-up notification preference from the current user's profile.
    /// </summary>
    private async Task LoadPopupPreferenceAsync()
    {
        try
        {
            var result = await AuthService.GetCurrentUserAsync();
            if (result.Succeeded && result.Data is not null)
            {
                _notificationPopupsEnabled = result.Data.NotificationPopupsEnabled;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error loading pop-up notification preference.");
        }
    }

    /// <summary>
    /// Loads notification delivery preferences for all categories from the API.
    /// On failure, logs a warning and leaves the list empty (non-blocking).
    /// </summary>
    private async Task LoadNotificationPreferencesAsync()
    {
        try
        {
            var result = await NotificationService.GetPreferencesAsync();
            if (result.Succeeded && result.Data is not null)
            {
                _notificationPreferences = result.Data;
            }
            else
            {
                Logger.LogWarning("Failed to load notification preferences.");
                Snackbar.Add("Failed to load notification preferences.", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error loading notification preferences.");
            Snackbar.Add("Failed to load notification preferences.", Severity.Error);
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Converts a <see cref="NotificationCategory"/> enum value to a human-readable display name.
    /// Inserts spaces before uppercase letters in multi-word enum names (e.g., "UserManagement" → "User Management").
    /// </summary>
    /// <param name="category">The notification category to format.</param>
    /// <returns>A human-readable category name.</returns>
    private static string FormatCategoryName(NotificationCategory category)
    {
        return category switch
        {
            NotificationCategory.Activity => "Activity",
            _ => category.ToString()
        };
    }

    /// <summary>
    /// Returns a human-readable description for each notification category,
    /// displayed as secondary text in the settings row layout.
    /// </summary>
    /// <param name="category">The notification category to describe.</param>
    /// <returns>A descriptive string for the category.</returns>
    private static string GetCategoryDescription(NotificationCategory category) => category switch
    {
        NotificationCategory.System => "Maintenance windows, platform updates, and downtime notices",
        NotificationCategory.Account => "Password expiry reminders and login alerts",
        NotificationCategory.Activity => "Task assignments, mentions, and workflow updates",
        _ => ""
    };

    #endregion
}
